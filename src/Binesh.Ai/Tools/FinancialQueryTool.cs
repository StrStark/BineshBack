using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Application.Abstractions;
using Binesh.Domain.Financial;

namespace Binesh.Ai.Tools;

public sealed class FinancialQueryTool(SchemaRegistry registry, AiQueryEngine engine, IBineshDbContext db)
    : QueryableToolBase<FinancialEntry>(registry.Get("Financial"), engine, db)
{
    public override string ToolName => "query_financial";
    public override string Description =>
        "Fetches accounting / chart-of-account data. Use for questions about debit (بدهکار), " +
        "credit (بستانکار), account codes, account names, or account types. " +
        "The Persian terms Bedehkar and Bestankar map to the English fields Debit and Credit.";

    protected override IQueryable<FinancialEntry> Source(IBineshDbContext db) => db.FinancialEntries;
}
