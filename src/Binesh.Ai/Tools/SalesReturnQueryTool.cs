using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Application.Abstractions;
using Binesh.Domain.Sales;

namespace Binesh.Ai.Tools;

public sealed class SalesReturnQueryTool(SchemaRegistry registry, AiQueryEngine engine, IBineshDbContext db)
    : QueryableToolBase<SalesReturn>(registry.Get("SalesReturn"), engine, db)
{
    public override string ToolName => "query_salesreturn";
    public override string Description =>
        "Fetches sales-return (refund) data. Use for questions about returned products, " +
        "refund prices, quantities returned, who returned them, or when refunds happened.";

    protected override IQueryable<SalesReturn> Source(IBineshDbContext db) => db.SalesReturns;
}
