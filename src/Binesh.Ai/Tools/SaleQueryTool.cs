using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Application.Abstractions;
using Binesh.Domain.Sales;

namespace Binesh.Ai.Tools;

public sealed class SaleQueryTool(SchemaRegistry registry, AiQueryEngine engine, IBineshDbContext db)
    : QueryableToolBase<Sale>(registry.Get("Sale"), engine, db)
{
    public override string ToolName => "query_sale";
    public override string Description =>
        "Fetches sales (revenue) data. Use for questions about prices, quantities, delivery, " +
        "invoice numbers, sale dates, which products sold, or which customers bought what. " +
        "For refunds use query_salesreturn.";

    protected override IQueryable<Sale> Source(IBineshDbContext db) => db.Sales;
}
