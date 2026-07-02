namespace Binesh.Domain.Financial;

/// <summary>
/// Singleton settings record mapping <see cref="FinancialEntry.Name"/>
/// values to the panel's balance-sheet and P&amp;L line items. Each
/// category is a list of <see cref="DetailedItem"/> (preserving the old
/// shape — only <c>Title</c> is consumed today, <c>Value</c> is reserved
/// for future tooling).
///
/// The old code allowed multiple rows in the settings table and rejected
/// any aggregation request that found more than one; we model it as a
/// genuine singleton so the upsert endpoint can't create duplicates.
/// </summary>
public sealed class FinancialMappingSettings
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }

    public IReadOnlyList<DetailedItem> OperationalCost { get; private set; } = [];
    public IReadOnlyList<DetailedItem> Payables { get; private set; } = [];
    public IReadOnlyList<DetailedItem> ToCalculateSales { get; private set; } = [];
    public IReadOnlyList<DetailedItem> ToCalculateLiquidity { get; private set; } = [];
    public IReadOnlyList<DetailedItem> ToCalculateGrossProfitLoss { get; private set; } = [];
    public IReadOnlyList<DetailedItem> ToCalculateOperatingProfitLoss { get; private set; } = [];

    /// <summary>Typo preserved from old schema (<c>BeforTax</c>) for parity. Map to "BeforeTax" client-side if desired.</summary>
    public IReadOnlyList<DetailedItem> ToCalculateProfitLossBeforTax { get; private set; } = [];

    public IReadOnlyList<DetailedItem> ToCalculateNetProfitLoss { get; private set; } = [];
    public IReadOnlyList<DetailedItem> ToCalculateAccumulatedProfitLoss { get; private set; } = [];
    public IReadOnlyList<DetailedItem> ToCalculateEquity { get; private set; } = [];

    // EF Core
    private FinancialMappingSettings() { }

    public static FinancialMappingSettings Create(
        Guid companyId,
        IReadOnlyList<DetailedItem>? operationalCost = null,
        IReadOnlyList<DetailedItem>? payables = null,
        IReadOnlyList<DetailedItem>? toCalculateSales = null,
        IReadOnlyList<DetailedItem>? toCalculateLiquidity = null,
        IReadOnlyList<DetailedItem>? toCalculateGrossProfitLoss = null,
        IReadOnlyList<DetailedItem>? toCalculateOperatingProfitLoss = null,
        IReadOnlyList<DetailedItem>? toCalculateProfitLossBeforTax = null,
        IReadOnlyList<DetailedItem>? toCalculateNetProfitLoss = null,
        IReadOnlyList<DetailedItem>? toCalculateAccumulatedProfitLoss = null,
        IReadOnlyList<DetailedItem>? toCalculateEquity = null)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
        }

        return new FinancialMappingSettings
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            OperationalCost = operationalCost ?? [],
            Payables = payables ?? [],
            ToCalculateSales = toCalculateSales ?? [],
            ToCalculateLiquidity = toCalculateLiquidity ?? [],
            ToCalculateGrossProfitLoss = toCalculateGrossProfitLoss ?? [],
            ToCalculateOperatingProfitLoss = toCalculateOperatingProfitLoss ?? [],
            ToCalculateProfitLossBeforTax = toCalculateProfitLossBeforTax ?? [],
            ToCalculateNetProfitLoss = toCalculateNetProfitLoss ?? [],
            ToCalculateAccumulatedProfitLoss = toCalculateAccumulatedProfitLoss ?? [],
            ToCalculateEquity = toCalculateEquity ?? [],
        };
    }

    /// <summary>Wholesale replace — settings are uploaded as one document.</summary>
    public void Replace(
        IReadOnlyList<DetailedItem> operationalCost,
        IReadOnlyList<DetailedItem> payables,
        IReadOnlyList<DetailedItem> toCalculateSales,
        IReadOnlyList<DetailedItem> toCalculateLiquidity,
        IReadOnlyList<DetailedItem> toCalculateGrossProfitLoss,
        IReadOnlyList<DetailedItem> toCalculateOperatingProfitLoss,
        IReadOnlyList<DetailedItem> toCalculateProfitLossBeforTax,
        IReadOnlyList<DetailedItem> toCalculateNetProfitLoss,
        IReadOnlyList<DetailedItem> toCalculateAccumulatedProfitLoss,
        IReadOnlyList<DetailedItem> toCalculateEquity)
    {
        OperationalCost = operationalCost;
        Payables = payables;
        ToCalculateSales = toCalculateSales;
        ToCalculateLiquidity = toCalculateLiquidity;
        ToCalculateGrossProfitLoss = toCalculateGrossProfitLoss;
        ToCalculateOperatingProfitLoss = toCalculateOperatingProfitLoss;
        ToCalculateProfitLossBeforTax = toCalculateProfitLossBeforTax;
        ToCalculateNetProfitLoss = toCalculateNetProfitLoss;
        ToCalculateAccumulatedProfitLoss = toCalculateAccumulatedProfitLoss;
        ToCalculateEquity = toCalculateEquity;
    }
}
