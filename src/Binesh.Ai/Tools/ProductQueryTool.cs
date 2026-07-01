using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Application.Abstractions;
using Binesh.Domain.Products;

namespace Binesh.Ai.Tools;

public sealed class ProductQueryTool(SchemaRegistry registry, AiQueryEngine engine, IBineshDbContext db)
    : QueryableToolBase<Product>(registry.Get("Product"), engine, db)
{
    public override string ToolName => "query_product";
    public override string Description =>
        "Fetches product (catalog) data. Use for questions about product codes, descriptions, " +
        "product types (Carpet, Rug, RawMaterials), detailed types, or when products were created. " +
        "For sales OF products use query_sale; for stock movements use query_inventoryevent.";

    protected override IQueryable<Product> Source(IBineshDbContext db) => db.Products;
}
