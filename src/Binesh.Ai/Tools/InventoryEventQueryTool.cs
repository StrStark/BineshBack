using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Application.Abstractions;
using Binesh.Domain.Products;

namespace Binesh.Ai.Tools;

public sealed class InventoryEventQueryTool(SchemaRegistry registry, AiQueryEngine engine, IBineshDbContext db)
    : QueryableToolBase<InventoryEvent>(registry.Get("InventoryEvent"), engine, db)
{
    public override string ToolName => "query_inventoryevent";
    public override string Description =>
        "Fetches inventory ledger events — stock movements (Receipt, Issue, ProformaInvoice, etc.). " +
        "Use for questions about stock arriving or leaving, quantities, unit prices, total prices, " +
        "or invoice numbers driving inventory changes.";

    protected override IQueryable<InventoryEvent> Source(IBineshDbContext db) => db.InventoryEvents;
}
