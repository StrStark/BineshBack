using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Analytics.Shared;
using Binesh.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Binesh.Infrastructure.Bi;

internal sealed class BiAnalyticsService(IOptions<BiSourceSettings> options) : IBiAnalyticsService
{
    private static readonly IReadOnlyList<ChartTypeDto> ChartTypes =
    [
        new("bar", "میله‌ای", "BarChart"),
        new("line", "خطی", "LineChart"),
        new("pie", "دایره‌ای", "PieChart"),
        new("area", "منطقه‌ای", "AreaChart"),
        new("table", "جدول", "Table"),
        new("card", "کارت", "CreditCard"),
        new("map", "نقشه", "Map"),
    ];

    private static readonly IReadOnlyList<string> Aggregations = ["sum", "avg", "count", "min", "max"];
    private static readonly IReadOnlyList<string> CountAggregation = ["count"];
    private static readonly IReadOnlyList<string> TextFilterOperators = ["equals", "not_equals", "contains", "not_contains"];
    private static readonly IReadOnlyList<string> RangeFilterOperators = ["equals", "not_equals", "gt", "gte", "lt", "lte"];
    private static readonly IReadOnlyList<string> BooleanFilterOperators = ["equals", "not_equals"];

    private readonly SqlServerSourceSettings _source = options.Value.DefaultSqlServer;

    public string DefaultSourceId => _source.Id;

    public IReadOnlyList<BiDataSourceDto> ListSources() =>
        BuildDatasets()
            .Where(d => d.Visible)
            .Select(d => d.ToSourceDto(_source.Enabled))
            .ToList();

    public BiDataSourceDetailDto GetSource(string sourceId)
    {
        var datasets = BuildDatasets();
        EnsureSource(sourceId, datasets);
        return RequireVisibleDataset(sourceId, datasets).ToDetailDto(_source.Enabled);
    }

    public BiSourceSchemaDto GetSchema(string sourceId)
    {
        var datasets = BuildDatasets();
        EnsureSource(sourceId, datasets);

        var selected = IsDefaultSource(sourceId)
            ? datasets.Where(d => d.Visible).ToList()
            : [RequireVisibleDataset(sourceId, datasets)];

        return new BiSourceSchemaDto(
            IsDefaultSource(sourceId) ? _source.Id : selected[0].Id,
            selected.Select(d => d.ToDatasetDto()).ToList(),
            ChartTypes,
            Aggregations);
    }

    public async Task<AnalyticsQueryResult> ExecuteAsync(
        AnalyticsQueryRequest request,
        CancellationToken cancellationToken)
    {
        var datasets = BuildDatasets();
        EnsureSource(request.SourceId, datasets);

        var datasetId = ResolveDatasetId(request, datasets);
        var dataset = RequireDataset(datasetId, datasets);
        var normalizedRequest = request with
        {
            SourceId = _source.Id,
            DatasetId = dataset.Id,
        };

        var parameters = new List<SqlParameter>();
        var sql = BuildSql(dataset, normalizedRequest, parameters);

        await using var connection = new SqlConnection(_source.BuildConnectionString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.CommandTimeout = _source.CommandTimeout;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        var rows = new List<IReadOnlyDictionary<string, object?>>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i, cancellationToken)
                    ? null
                    : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return new AnalyticsQueryResult(_source.Id, dataset.Id, rows);
    }

    private void EnsureSource(string? sourceId, IReadOnlyList<DatasetDefinition> datasets)
    {
        if (!_source.Enabled)
        {
            throw new ConflictException("The SQL Server BI source is disabled.", "bi.source_disabled");
        }

        if (string.IsNullOrWhiteSpace(sourceId)
            || IsDefaultSource(sourceId)
            || datasets.Any(d => d.Matches(sourceId)))
        {
            return;
        }

        throw new NotFoundException("DataSource", sourceId);
    }

    private bool IsDefaultSource(string? sourceId) =>
        string.IsNullOrWhiteSpace(sourceId)
        || string.Equals(sourceId, _source.Id, StringComparison.OrdinalIgnoreCase);

    private static string ResolveDatasetId(
        AnalyticsQueryRequest request,
        IReadOnlyList<DatasetDefinition> datasets)
    {
        var datasetId = FirstNonBlank(request.DatasetId, request.Table);
        if (string.IsNullOrWhiteSpace(datasetId)
            && !string.IsNullOrWhiteSpace(request.SourceId)
            && datasets.Any(d => d.Matches(request.SourceId)))
        {
            datasetId = request.SourceId;
        }

        if (string.IsNullOrWhiteSpace(datasetId))
        {
            throw new ConflictException("Dataset is required.", "bi.dataset_required");
        }

        return datasetId;
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static DatasetDefinition RequireVisibleDataset(
        string sourceId,
        IReadOnlyList<DatasetDefinition> datasets) =>
        datasets.SingleOrDefault(d => d.Visible && d.Matches(sourceId))
        ?? throw new NotFoundException("DataSource", sourceId);

    private static DatasetDefinition RequireDataset(
        string datasetId,
        IReadOnlyList<DatasetDefinition> datasets) =>
        datasets.SingleOrDefault(d => d.Matches(datasetId))
        ?? throw new ConflictException($"Dataset '{datasetId}' is not available.", "bi.dataset_not_allowed");

    private string BuildSql(
        DatasetDefinition dataset,
        AnalyticsQueryRequest request,
        List<SqlParameter> parameters)
    {
        var limit = Math.Clamp(request.Limit ?? 500, 1, 5000);
        var where = BuildWhere(dataset, request.Filters, parameters);
        var groupBy = (request.GroupBy ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => dataset.RequireField(f).Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (groupBy.Count == 0 && !string.IsNullOrWhiteSpace(request.LabelField))
        {
            groupBy.Add(dataset.RequireField(request.LabelField).Name);
        }

        var values = (request.Values ?? []).ToList();
        if (values.Count == 0 && !string.IsNullOrWhiteSpace(request.ValueField))
        {
            values.Add(new AnalyticsValueDto(
                request.ValueField,
                request.Aggregation ?? "sum",
                request.ValueField));
        }

        var baseSql = dataset.Sql.Trim().TrimEnd(';');
        var outer = new StringBuilder();
        if (groupBy.Count > 0 || values.Count > 0)
        {
            var selectParts = new List<string>();
            selectParts.AddRange(groupBy.Select(f => $"{QuoteField(f)} AS {QuoteField(f)}"));
            if (values.Count == 0)
            {
                selectParts.Add("COUNT_BIG(*) AS [count]");
            }
            else
            {
                foreach (var value in values)
                {
                    var field = dataset.RequireField(value.Field);
                    var fn = NormalizeAggregation(value.Aggregation);
                    if (fn != "count" && field.Type != "number")
                    {
                        throw new ConflictException($"Aggregation '{fn}' can only be used with numeric field '{field.Name}'.");
                    }

                    var alias = SafeAlias(value.Alias, $"{fn}_{field.Name}");
                    var expression = fn == "count"
                        ? $"COUNT({QuoteField(field.Name)})"
                        : $"{fn.ToUpperInvariant()}(TRY_CONVERT(float, {QuoteField(field.Name)}))";
                    selectParts.Add($"{expression} AS {QuoteField(alias)}");
                }
            }

            outer.Append("SELECT TOP (").Append(limit.ToString(CultureInfo.InvariantCulture)).Append(") ");
            outer.AppendJoin(", ", selectParts);
            outer.AppendLine();
            outer.Append("FROM (").AppendLine(baseSql).AppendLine(") AS q");
            if (where.Length > 0) { outer.Append("WHERE ").AppendLine(where); }
            if (groupBy.Count > 0)
            {
                outer.Append("GROUP BY ").AppendJoin(", ", groupBy.Select(QuoteField)).AppendLine();
            }
        }
        else
        {
            outer.Append("SELECT TOP (").Append(limit.ToString(CultureInfo.InvariantCulture)).Append(") ");
            outer.AppendJoin(", ", dataset.Fields.Select(f => $"{QuoteField(f.Name)} AS {QuoteField(f.Name)}"));
            outer.AppendLine();
            outer.Append("FROM (").AppendLine(baseSql).AppendLine(") AS q");
            if (where.Length > 0) { outer.Append("WHERE ").AppendLine(where); }
        }

        if (request.OrderBy is not null && !string.IsNullOrWhiteSpace(request.OrderBy.Field))
        {
            var direction = string.Equals(request.OrderBy.Direction, "desc", StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "ASC";
            var orderField = request.OrderBy.Field;
            if (dataset.Fields.All(f => !string.Equals(f.Name, orderField, StringComparison.OrdinalIgnoreCase))
                && values.All(v => !string.Equals(v.Alias, orderField, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ConflictException($"Order field '{orderField}' is not part of the dataset/query result.");
            }
            outer.Append("ORDER BY ").Append(QuoteField(orderField)).Append(' ').Append(direction).AppendLine();
        }

        return outer.ToString();
    }

    private static string BuildWhere(
        DatasetDefinition dataset,
        IReadOnlyList<AnalyticsFilterDto>? filters,
        List<SqlParameter> parameters)
    {
        if (filters is null || filters.Count == 0) { return string.Empty; }

        var clauses = new List<string>();
        for (var i = 0; i < filters.Count; i++)
        {
            var filter = filters[i];
            var field = dataset.RequireField(filter.Field);
            var parameterName = $"@p{i}";
            var value = ConvertJson(filter.Value);
            var column = QuoteField(field.Name);
            var op = filter.Operator.Trim().ToLowerInvariant();

            switch (op)
            {
                case "equals":
                case "eq":
                    if (value is null)
                    {
                        clauses.Add($"{column} IS NULL");
                    }
                    else
                    {
                        clauses.Add($"{column} = {parameterName}");
                        parameters.Add(new SqlParameter(parameterName, value));
                    }
                    break;
                case "not_equals":
                case "notequals":
                case "ne":
                case "not":
                    if (value is null)
                    {
                        clauses.Add($"{column} IS NOT NULL");
                    }
                    else
                    {
                        clauses.Add($"{column} <> {parameterName}");
                        parameters.Add(new SqlParameter(parameterName, value));
                    }
                    break;
                case "contains":
                    clauses.Add($"{column} LIKE {parameterName}");
                    parameters.Add(new SqlParameter(parameterName, $"%{value}%"));
                    break;
                case "not_contains":
                case "notcontains":
                    clauses.Add($"{column} NOT LIKE {parameterName}");
                    parameters.Add(new SqlParameter(parameterName, $"%{value}%"));
                    break;
                case "gt":
                case "greater":
                case "greater_than":
                case "greaterthan":
                    clauses.Add($"{column} > {parameterName}");
                    parameters.Add(new SqlParameter(parameterName, value ?? DBNull.Value));
                    break;
                case "gte":
                case "ge":
                case "greatereq":
                case "greater_than_or_equal":
                case "greaterthanorequal":
                    clauses.Add($"{column} >= {parameterName}");
                    parameters.Add(new SqlParameter(parameterName, value ?? DBNull.Value));
                    break;
                case "lt":
                case "less":
                case "less_than":
                case "lessthan":
                    clauses.Add($"{column} < {parameterName}");
                    parameters.Add(new SqlParameter(parameterName, value ?? DBNull.Value));
                    break;
                case "lte":
                case "le":
                case "lesseq":
                case "less_than_or_equal":
                case "lessthanorequal":
                    clauses.Add($"{column} <= {parameterName}");
                    parameters.Add(new SqlParameter(parameterName, value ?? DBNull.Value));
                    break;
                default:
                    throw new ConflictException($"Filter operator '{filter.Operator}' is not supported.");
            }
        }

        return string.Join(" AND ", clauses);
    }

    private IReadOnlyList<DatasetDefinition> BuildDatasets()
    {
        var anbar = QuoteDatabase(_source.AnbarDatabase);
        var hesab = QuoteDatabase(_source.HesabDatabase);

        return
        [
            new DatasetDefinition(
                "Sale",
                "فروش",
                "ردیف‌های فروش تاییدشده از سند حسابداری و حواله انبار",
                $"""
                SELECT
                    s.SanadNo AS DocNum,
                    s.HesabID AS AccountCode,
                    s.ArticleDesc AS Article,
                    s.FactorNum AS HavaleCode,
                    rh.RHTCode AS Type,
                    rt.RHTDescHavale AS TypeTitle,
                    rh.RHSanadDate AS [Date],
                    rhi.PRGCode,
                    rhi.KCode AS KalaCode,
                    k.KCodeDelimiter AS KalDelimiter,
                    k.KDesc,
                    rhi.RHIUCode1 AS UnitCode1,
                    rhi.RHIUValue1 AS UnitValue1,
                    rhi.RHIUCode2 AS UnitCode2,
                    rhi.RHIUValue2 AS UnitValue2,
                    rhi.RHIUCode3 AS UnitCode3,
                    rhi.RHIUValue3 AS UnitValue3,
                    rhi.RHIFi AS UnitPrice,
                    rhi.RHIPrice AS LinePrice,
                    rh.RHCodeSenderReciver AS TarafHesab,
                    p.PGCode AS CustomerType,
                    pg.PGDesc AS CustomerTypeTitle,
                    p.PHesabID AS CustomerAccount,
                    p.PName AS CustomerName,
                    p.PFamily AS CustomerFamily,
                    p.PGCodeComputed AS CustomerCode,
                    p.PCodeMeliOrSH_Sabt AS CustomerIdentifier,
                    p.PTel AS CustomerTel,
                    p.PMobile AS CustomerPhoneNumber,
                    p.PEmail AS CustomerEmail,
                    p.PAddress AS CustomerAddress,
                    p.PBirthday AS CustomerBirthDay,
                    p.PEnable AS Active,
                    sg.SBGDesc AS DetailedType,
                    s.BestankarSum AS AccountingSaleAmount,
                    s.YearID,
                    rhi.RHIRadif AS ItemRow
                FROM (
                    SELECT s.*
                    FROM {hesab}.dbo.SanadData AS s
                    WHERE s.HesabID LIKE '6010000003%'
                      AND s.FactorNum IS NOT NULL
                      AND s.FactorType = 2
                      AND s.OperationCode = 60
                ) AS s
                INNER JOIN {anbar}.dbo.ResidHavale AS rh
                    ON rh.RHAction = 2
                   AND rh.RHCode = TRY_CONVERT(decimal(10, 2), s.FactorNum)
                   AND rh.ACode = s.AnbarCode
                   AND rh.RHYearID = s.YearID
                   AND rh.RHSanadDate = s.OperationDate
                INNER JOIN (
                    SELECT RHTCode
                    FROM {anbar}.dbo.ResidHavaleType
                    WHERE RHTDescHavale LIKE N'%فروش%'
                      AND RHTDescHavale NOT LIKE N'%درخواست%'
                ) AS st
                    ON st.RHTCode = rh.RHTCode
                INNER JOIN {anbar}.dbo.ResidHavaleItems AS rhi
                    ON rhi.RHAction = rh.RHAction
                   AND rhi.RHCode = rh.RHCode
                   AND rhi.ACode = rh.ACode
                   AND rhi.RHIYearID = rh.RHYearID
                INNER JOIN {anbar}.dbo.Kala AS k
                    ON k.PRGCode = rhi.PRGCode
                   AND k.KCode = rhi.KCode
                LEFT JOIN {anbar}.dbo.ResidHavaleType AS rt
                    ON rt.RHTCode = rh.RHTCode
                LEFT JOIN {anbar}.dbo.Person AS p
                    ON p.PGCodeComputed = rh.RHCodeSenderReciver
                LEFT JOIN {anbar}.dbo.PersonGroup AS pg
                    ON pg.PGCode = p.PGCode
                OUTER APPLY (
                    SELECT TOP (1)
                        TRY_CONVERT(int, LEFT(ss.value, CHARINDEX('-', ss.value) - 1)) AS CreationType
                    FROM STRING_SPLIT(k.KCodeDelimiter, ';') AS ss
                    WHERE CHARINDEX('-', ss.value) > 0
                      AND RIGHT(ss.value, LEN(ss.value) - CHARINDEX('-', ss.value)) =
                          CASE
                              WHEN k.PRGCode = 1 THEN '8'
                              WHEN k.PRGCode IN (2, 6) THEN '1'
                          END
                ) AS ct
                LEFT JOIN {anbar}.dbo.SubGroup AS sg
                    ON sg.PRGCode = k.PRGCode
                   AND sg.SBGCode = ct.CreationType
                   AND sg.SGCode =
                       CASE
                           WHEN k.PRGCode = 1 THEN 8
                           WHEN k.PRGCode IN (2, 6) THEN 1
                       END
                """,
                SaleFields()),
            new DatasetDefinition(
                "Product",
                "محصولات",
                "حرکت کالا در اسناد انبار با گروه و نوع تفصیلی محصول",
                $"""
                SELECT
                    CASE rh.RHAction
                        WHEN 1 THEN N'Receipt'
                        WHEN 2 THEN N'Issue'
                        WHEN 3 THEN N'SalesOrConsumptionRequest'
                        WHEN 4 THEN N'PurchaseOrProductionRequest'
                        WHEN 5 THEN N'ProformaInvoice'
                        WHEN 6 THEN N'SalesInvoice'
                    END AS RHActionDesc,
                    pr.PRGDesc AS [Type],
                    sg.SBGDesc AS DetailedType,
                    REPLACE(a.ADesc, N'*', N'') AS Anbar,
                    k.PRGCode,
                    k.KCode AS KalaCode,
                    k.KCodeDelimiter,
                    k.KDesc,
                    rh.RHSanadDate AS [Date],
                    rh.RegistrationDate,
                    rhi.RHCode AS FactorNum,
                    rhi.RHIRadif AS ItemRow,
                    rhi.RHIUCode1 AS UnitCode1,
                    rhi.RHIUValue1 AS UnitValue1,
                    rhi.RHIUCode2 AS UnitCode2,
                    rhi.RHIUValue2 AS UnitValue2,
                    rhi.RHIUCode3 AS UnitCode3,
                    rhi.RHIUValue3 AS UnitValue3,
                    rhi.RHIFi AS Fee,
                    rhi.RHIPrice AS Price,
                    rh.RHDesc,
                    rh.RHYearID
                FROM {anbar}.dbo.ResidHavaleItems AS rhi
                JOIN {anbar}.dbo.ResidHavale AS rh
                    ON rh.RHCode = rhi.RHCode
                   AND rh.RHAction = rhi.RHAction
                   AND rh.ACode = rhi.ACode
                   AND rh.RHYearID = rhi.RHIYearID
                JOIN {anbar}.dbo.Kala AS k
                    ON k.PRGCode = rhi.PRGCode
                   AND k.KCode = rhi.KCode
                JOIN {anbar}.dbo.ResidHavaleType AS rht
                    ON rht.RHTCode = rh.RHTCode
                JOIN {anbar}.dbo.Anbars AS a
                    ON a.ACode = rhi.ACode
                JOIN {anbar}.dbo.PrimaryGroup AS pr
                    ON pr.PRGCode = rhi.PRGCode
                OUTER APPLY (
                    SELECT TOP (1)
                        TRY_CONVERT(int, LEFT(ss.value, CHARINDEX('-', ss.value) - 1)) AS CreationType
                    FROM STRING_SPLIT(k.KCodeDelimiter, ';') AS ss
                    WHERE CHARINDEX('-', ss.value) > 0
                      AND RIGHT(ss.value, LEN(ss.value) - CHARINDEX('-', ss.value)) =
                          CASE
                              WHEN k.PRGCode = 1 THEN '8'
                              WHEN k.PRGCode IN (2, 6) THEN '1'
                          END
                ) AS ct
                LEFT JOIN {anbar}.dbo.SubGroup AS sg
                    ON sg.PRGCode = k.PRGCode
                   AND sg.SBGCode = ct.CreationType
                   AND sg.SGCode =
                       CASE
                           WHEN k.PRGCode = 1 THEN 8
                           WHEN k.PRGCode IN (2, 6) THEN 1
                       END
                WHERE k.PRGCode IN (1, 2, 4, 6)
                """,
                ProductFields()),
            new DatasetDefinition(
                "Customer",
                "مشتریان",
                "اشخاص و حساب‌های طرف حساب متصل به مشتریان",
                $"""
                SELECT
                    p.PGCodeComputed AS CustomerPersonID,
                    COALESCE(
                        NULLIF(LTRIM(RTRIM(p.PCodeMeliOrSH_Sabt)), ''),
                        NULLIF(LTRIM(RTRIM(p.PMobile)), ''),
                        NULLIF(LTRIM(RTRIM(CONCAT(ISNULL(p.PName, ''), N' ', ISNULL(p.PFamily, '')))), '')
                    ) AS CustomerGroupKey,
                    p.PGCode AS CustomerType,
                    pg.PGDesc AS CustomerTypeTitle,
                    p.PCode AS PersonCodeInGroup,
                    p.PName AS CustomerName,
                    p.PFamily AS CustomerFamily,
                    LTRIM(RTRIM(CONCAT(ISNULL(p.PName, ''), N' ', ISNULL(p.PFamily, '')))) AS CustomerFullName,
                    p.PCodeMeliOrSH_Sabt AS CustomerIdentifier,
                    p.PTel AS CustomerTel,
                    p.PMobile AS CustomerPhoneNumber,
                    p.PEmail AS CustomerEmail,
                    p.PAddress AS CustomerAddress,
                    p.PBirthday AS CustomerBirthDay,
                    p.PEnable AS Active,
                    p.PHesabID AS LinkedHesabID,
                    h.HesabID,
                    h.TafsiliCode AS CounterPartyCode,
                    h.Name AS HesabName,
                    h.HesabName AS FullHesabName,
                    h.StructureID,
                    hs.StructureName,
                    SUBSTRING(p.PHesabID, 11, 1) AS TafsiliPrefix
                FROM {anbar}.dbo.Person AS p
                LEFT JOIN {anbar}.dbo.PersonGroup AS pg
                    ON pg.PGCode = p.PGCode
                LEFT JOIN {hesab}.dbo.Hesabs AS h
                    ON h.HesabID = p.PHesabID
                LEFT JOIN {hesab}.dbo.HesabStructures AS hs
                    ON hs.StructureID = h.StructureID
                WHERE p.PHesabID IS NOT NULL
                  AND p.PGCode IN (1, 7)
                """,
                CustomerFields()),
            new DatasetDefinition(
                "Financial",
                "مالی",
                "حساب‌های کل و مانده‌های بدهکار/بستانکار تجمیع‌شده",
                $"""
                SELECT
                    k.HesabID AS Id,
                    k.KolCode AS Code,
                    k.Name,
                    k.HesabName,
                    k.GroupName AS [Type],
                    k.GroupID,
                    k.GroupTypeID,
                    ISNULL(m.Bedehkar, 0) AS Bedehkar,
                    ISNULL(m.Bestankar, 0) AS Bestankar,
                    ISNULL(m.Bedehkar, 0) - ISNULL(m.Bestankar, 0) AS DebitMinusCredit,
                    CASE
                        WHEN k.GroupID IN (1, 2, 7, 8) THEN ISNULL(m.Bedehkar, 0) - ISNULL(m.Bestankar, 0)
                        WHEN k.GroupID IN (3, 4, 5, 6) THEN ISNULL(m.Bestankar, 0) - ISNULL(m.Bedehkar, 0)
                        ELSE ISNULL(m.Bedehkar, 0) - ISNULL(m.Bestankar, 0)
                    END AS NormalBalance
                FROM (
                    SELECT
                        h.HesabID,
                        h.KolCode,
                        h.Name,
                        h.HesabName,
                        h.GroupID,
                        gr.GroupName,
                        gr.GroupTypeID
                    FROM {hesab}.dbo.Hesabs AS h
                    JOIN {hesab}.dbo.HesabGroups AS gr
                        ON gr.GroupID = h.GroupID
                    WHERE h.StructureID = 1
                ) AS k
                LEFT JOIN (
                    SELECT
                        h.KolCode,
                        SUM(sd.BedehkarSum) AS Bedehkar,
                        SUM(sd.BestankarSum) AS Bestankar
                    FROM {hesab}.dbo.SanadData AS sd
                    JOIN {hesab}.dbo.Hesabs AS h
                        ON h.HesabID = sd.HesabID
                    GROUP BY h.KolCode
                ) AS m
                    ON m.KolCode = k.KolCode
                """,
                FinancialFields(),
                Aliases: ["FinancialTransaction"]),
            new DatasetDefinition(
                "WarehouseItem",
                "موجودی انبار",
                "موجودی تجمیعی کالاها برای endpointهای انبار",
                $"""
                SELECT
                    ISNULL(k.KDesc, N'') AS productName,
                    ISNULL(pr.PRGDesc, N'') AS category,
                    REPLACE(ISNULL(a.ADesc, N''), N'*', N'') AS warehouse,
                    SUM(CASE WHEN rh.RHAction = 1 THEN ISNULL(rhi.RHIUValue1, 0) WHEN rh.RHAction = 2 THEN -ISNULL(rhi.RHIUValue1, 0) ELSE 0 END) AS quantity,
                    CAST(rhi.RHIUCode1 AS nvarchar(32)) AS unit,
                    N'normal' AS status,
                    CAST(0 AS float) AS minStock,
                    CAST(0 AS float) AS maxStock,
                    MAX(rh.RHSanadDate) AS lastUpdate
                FROM {anbar}.dbo.ResidHavaleItems AS rhi
                JOIN {anbar}.dbo.ResidHavale AS rh
                    ON rh.RHCode = rhi.RHCode
                   AND rh.RHAction = rhi.RHAction
                   AND rh.ACode = rhi.ACode
                   AND rh.RHYearID = rhi.RHIYearID
                JOIN {anbar}.dbo.Kala AS k
                    ON k.PRGCode = rhi.PRGCode
                   AND k.KCode = rhi.KCode
                LEFT JOIN {anbar}.dbo.PrimaryGroup AS pr
                    ON pr.PRGCode = k.PRGCode
                LEFT JOIN {anbar}.dbo.Anbars AS a
                    ON a.ACode = rhi.ACode
                GROUP BY k.KDesc, pr.PRGDesc, a.ADesc, rhi.RHIUCode1
                """,
                WarehouseItemFields(),
                Visible: false),
            new DatasetDefinition(
                "WarehouseTransaction",
                "تراکنش‌های انبار",
                "ورودی و خروجی تجمیعی انبار برای endpointهای انبار",
                $"""
                SELECT
                    rh.RHSanadDate AS [date],
                    SUM(CASE WHEN rh.RHAction = 1 THEN ISNULL(rhi.RHIUValue1, 0) ELSE 0 END) AS [in],
                    SUM(CASE WHEN rh.RHAction = 2 THEN ISNULL(rhi.RHIUValue1, 0) ELSE 0 END) AS [out]
                FROM {anbar}.dbo.ResidHavaleItems AS rhi
                JOIN {anbar}.dbo.ResidHavale AS rh
                    ON rh.RHCode = rhi.RHCode
                   AND rh.RHAction = rhi.RHAction
                   AND rh.ACode = rhi.ACode
                   AND rh.RHYearID = rhi.RHIYearID
                GROUP BY rh.RHSanadDate
                """,
                WarehouseTransactionFields(),
                Visible: false),
        ];
    }

    private static IReadOnlyList<FieldDefinition> SaleFields() =>
    [
        Field("DocNum", "شماره سند", "number", "dimension"),
        Field("AccountCode", "کد حساب", "string"),
        Field("Article", "شرح سند", "string"),
        Field("HavaleCode", "شماره حواله", "string"),
        Field("Type", "نوع حواله", "number", "dimension"),
        Field("TypeTitle", "عنوان نوع حواله", "string"),
        Field("Date", "تاریخ", "date"),
        Field("PRGCode", "کد گروه کالا", "number", "dimension"),
        Field("KalaCode", "کد کالا", "number", "dimension"),
        Field("KalDelimiter", "کد تفصیلی کالا", "string"),
        Field("KDesc", "شرح کالا", "string"),
        Field("UnitCode1", "کد واحد ۱", "number", "dimension"),
        Field("UnitValue1", "مقدار واحد ۱", "number"),
        Field("UnitCode2", "کد واحد ۲", "number", "dimension"),
        Field("UnitValue2", "مقدار واحد ۲", "number"),
        Field("UnitCode3", "کد واحد ۳", "number", "dimension"),
        Field("UnitValue3", "مقدار واحد ۳", "number"),
        Field("UnitPrice", "فی واحد", "number"),
        Field("LinePrice", "مبلغ ردیف", "number"),
        Field("TarafHesab", "طرف حساب", "string"),
        Field("CustomerType", "نوع مشتری", "number", "dimension"),
        Field("CustomerTypeTitle", "عنوان نوع مشتری", "string"),
        Field("CustomerAccount", "حساب مشتری", "string"),
        Field("CustomerName", "نام مشتری", "string"),
        Field("CustomerFamily", "نام خانوادگی مشتری", "string"),
        Field("CustomerCode", "کد مشتری", "string"),
        Field("CustomerIdentifier", "شناسه مشتری", "string"),
        Field("CustomerTel", "تلفن مشتری", "string"),
        Field("CustomerPhoneNumber", "موبایل مشتری", "string"),
        Field("CustomerEmail", "ایمیل مشتری", "string"),
        Field("CustomerAddress", "آدرس مشتری", "string"),
        Field("CustomerBirthDay", "تاریخ تولد مشتری", "date"),
        Field("Active", "فعال", "boolean"),
        Field("DetailedType", "نوع تفصیلی", "string"),
        Field("AccountingSaleAmount", "مبلغ فروش حسابداری", "number"),
        Field("YearID", "سال مالی", "number", "dimension"),
        Field("ItemRow", "ردیف آیتم", "number", "dimension"),
    ];

    private static IReadOnlyList<FieldDefinition> ProductFields() =>
    [
        Field("RHActionDesc", "نوع عملیات", "string"),
        Field("Type", "گروه اصلی", "string"),
        Field("DetailedType", "نوع تفصیلی", "string"),
        Field("Anbar", "انبار", "string"),
        Field("PRGCode", "کد گروه کالا", "number", "dimension"),
        Field("KalaCode", "کد کالا", "number", "dimension"),
        Field("KCodeDelimiter", "کد تفصیلی کالا", "string"),
        Field("KDesc", "شرح کالا", "string"),
        Field("Date", "تاریخ سند", "date"),
        Field("RegistrationDate", "تاریخ ثبت", "date"),
        Field("FactorNum", "شماره فاکتور", "number", "dimension"),
        Field("ItemRow", "ردیف آیتم", "number", "dimension"),
        Field("UnitCode1", "کد واحد ۱", "number", "dimension"),
        Field("UnitValue1", "مقدار واحد ۱", "number"),
        Field("UnitCode2", "کد واحد ۲", "number", "dimension"),
        Field("UnitValue2", "مقدار واحد ۲", "number"),
        Field("UnitCode3", "کد واحد ۳", "number", "dimension"),
        Field("UnitValue3", "مقدار واحد ۳", "number"),
        Field("Fee", "فی", "number"),
        Field("Price", "مبلغ", "number"),
        Field("RHDesc", "شرح حواله", "string"),
        Field("RHYearID", "سال سند", "number", "dimension"),
    ];

    private static IReadOnlyList<FieldDefinition> CustomerFields() =>
    [
        Field("CustomerPersonID", "شناسه شخص", "number", "dimension"),
        Field("CustomerGroupKey", "کلید گروه مشتری", "string"),
        Field("CustomerType", "نوع مشتری", "number", "dimension"),
        Field("CustomerTypeTitle", "عنوان نوع مشتری", "string"),
        Field("PersonCodeInGroup", "کد شخص در گروه", "number", "dimension"),
        Field("CustomerName", "نام", "string"),
        Field("CustomerFamily", "نام خانوادگی", "string"),
        Field("CustomerFullName", "نام کامل", "string"),
        Field("CustomerIdentifier", "شناسه ملی/ثبت", "string"),
        Field("CustomerTel", "تلفن", "string"),
        Field("CustomerPhoneNumber", "موبایل", "string"),
        Field("CustomerEmail", "ایمیل", "string"),
        Field("CustomerAddress", "آدرس", "string"),
        Field("CustomerBirthDay", "تاریخ تولد", "date"),
        Field("Active", "فعال", "boolean"),
        Field("LinkedHesabID", "حساب متصل", "string"),
        Field("HesabID", "شناسه حساب", "string"),
        Field("CounterPartyCode", "کد طرف حساب", "string"),
        Field("HesabName", "نام حساب", "string"),
        Field("FullHesabName", "نام کامل حساب", "string"),
        Field("StructureID", "شناسه ساختار", "number", "dimension"),
        Field("StructureName", "نام ساختار", "string"),
        Field("TafsiliPrefix", "پیشوند تفصیلی", "string"),
    ];

    private static IReadOnlyList<FieldDefinition> FinancialFields() =>
    [
        Field("Id", "شناسه حساب", "string"),
        Field("Code", "کد کل", "string"),
        Field("Name", "نام", "string"),
        Field("HesabName", "نام حساب", "string"),
        Field("Type", "گروه حساب", "string"),
        Field("GroupID", "شناسه گروه", "number", "dimension"),
        Field("GroupTypeID", "شناسه نوع گروه", "number", "dimension"),
        Field("Bedehkar", "بدهکار", "number"),
        Field("Bestankar", "بستانکار", "number"),
        Field("DebitMinusCredit", "بدهکار منهای بستانکار", "number"),
        Field("NormalBalance", "مانده عادی", "number"),
    ];

    private static IReadOnlyList<FieldDefinition> WarehouseItemFields() =>
    [
        Field("productName", "نام محصول", "string"),
        Field("category", "دسته‌بندی", "string"),
        Field("warehouse", "انبار", "string"),
        Field("quantity", "موجودی", "number"),
        Field("unit", "واحد", "string"),
        Field("status", "وضعیت", "string"),
        Field("minStock", "حداقل موجودی", "number"),
        Field("maxStock", "حداکثر موجودی", "number"),
        Field("lastUpdate", "آخرین بروزرسانی", "date"),
    ];

    private static IReadOnlyList<FieldDefinition> WarehouseTransactionFields() =>
    [
        Field("date", "تاریخ", "date"),
        Field("in", "ورودی", "number"),
        Field("out", "خروجی", "number"),
    ];

    private static FieldDefinition Field(string name, string label, string type, string? role = null) =>
        new(name, label, type, role ?? (type == "number" ? "measure" : "dimension"));

    private static object? ConvertJson(JsonElement? value)
    {
        if (value is null) { return null; }
        var v = value.Value;
        return v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number when v.TryGetInt64(out var l) => l,
            JsonValueKind.Number when v.TryGetDouble(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => v.ToString(),
        };
    }

    private static string NormalizeAggregation(string? aggregation)
    {
        var value = (aggregation ?? "sum").Trim().ToLowerInvariant();
        if (!Aggregations.Contains(value))
        {
            throw new ConflictException($"Aggregation '{aggregation}' is not supported.");
        }
        return value;
    }

    private static string QuoteField(string field) => $"[{field.Replace("]", "]]")}]";

    private static string QuoteDatabase(string database)
    {
        if (string.IsNullOrWhiteSpace(database) || database.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
        {
            throw new InvalidOperationException($"Unsafe SQL Server database name '{database}'.");
        }
        return $"[{database.Replace("]", "]]")}]";
    }

    private static string SafeAlias(string? alias, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(alias) ? fallback : alias.Trim();
        if (value.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
        {
            throw new ConflictException($"Alias '{value}' contains unsupported characters.");
        }
        return value;
    }

    private static IReadOnlyList<string> AllowedAggregationsFor(FieldDefinition field) =>
        field.Role == "measure" ? Aggregations : CountAggregation;

    private static IReadOnlyList<string> FilterOperatorsFor(FieldDefinition field) =>
        field.Type switch
        {
            "number" or "date" => RangeFilterOperators,
            "boolean" => BooleanFilterOperators,
            _ => TextFilterOperators,
        };

    private sealed record DatasetDefinition(
        string Id,
        string Label,
        string Description,
        string Sql,
        IReadOnlyList<FieldDefinition> Fields,
        bool Visible = true,
        IReadOnlyList<string>? Aliases = null)
    {
        public bool Matches(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && (string.Equals(Id, value, StringComparison.OrdinalIgnoreCase)
                || (Aliases?.Any(alias => string.Equals(alias, value, StringComparison.OrdinalIgnoreCase)) ?? false));

        public BiDataSourceDto ToSourceDto(bool enabled)
        {
            var fields = Fields.Select(ToFieldDto).ToList();
            return new BiDataSourceDto(
                Id,
                Label,
                Description,
                "SqlServer",
                enabled,
                fields.Count(f => f.Role == "dimension"),
                fields.Count(f => f.Role == "measure"));
        }

        public BiDataSourceDetailDto ToDetailDto(bool enabled) => new(
            Id,
            Label,
            Description,
            "SqlServer",
            enabled,
            Fields.Select(ToFieldDto).ToList(),
            ChartTypes,
            Aggregations);

        public BiDatasetDto ToDatasetDto() => new(
            Id,
            Label,
            Fields.Select(ToFieldDto).ToList());

        public FieldDefinition RequireField(string field) =>
            Fields.SingleOrDefault(f => string.Equals(f.Name, field, StringComparison.OrdinalIgnoreCase))
            ?? throw new ConflictException($"Field '{field}' is not part of dataset '{Id}'.");

        private static BiFieldDto ToFieldDto(FieldDefinition field) => new(
            field.Name,
            field.Label,
            field.Type,
            field.Role,
            AllowedAggregationsFor(field),
            FilterOperatorsFor(field));
    }

    private sealed record FieldDefinition(string Name, string Label, string Type, string Role);
}
