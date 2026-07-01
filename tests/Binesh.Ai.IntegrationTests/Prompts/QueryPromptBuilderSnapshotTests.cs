using Binesh.Ai.Prompts;
using Binesh.Ai.QueryEngine;
using Binesh.Ai.Schemas;
using Binesh.Ai.Tools;

namespace Binesh.Ai.IntegrationTests.Prompts;

/// <summary>
/// Locks the exact string produced by <see cref="QueryPromptBuilder"/> for
/// the 6 built-in schemas. Any change to a schema, a tool description, or
/// the builder template breaks this test — by design, so prompt drift gets
/// reviewed deliberately.
///
/// <para>When this fails after an intentional schema/prompt change, copy
/// the actual output into <see cref="Expected"/> and commit alongside the
/// underlying change.</para>
/// </summary>
public sealed class QueryPromptBuilderSnapshotTests
{
    [Fact]
    public void Build_Snapshot_DoesNotDriftSilently()
    {
        var registry = new QueryToolRegistry();
        registry.Register(new SnapshotTool("query_customer", "Customer description", CustomerSchema.Build()));
        registry.Register(new SnapshotTool("query_product", "Product description", ProductSchema.Build()));
        registry.Register(new SnapshotTool("query_inventoryevent", "InventoryEvent description", InventoryEventSchema.Build()));
        registry.Register(new SnapshotTool("query_financial", "Financial description", FinancialSchema.Build()));
        registry.Register(new SnapshotTool("query_sale", "Sale description", SaleSchema.Build()));
        registry.Register(new SnapshotTool("query_salesreturn", "SalesReturn description", SalesReturnSchema.Build()));

        var actual = NormalizeLineEndings(QueryPromptBuilder.Build(registry));
        Assert.Equal(NormalizeLineEndings(Expected), actual);
    }

    private static string NormalizeLineEndings(string s) =>
        s.Replace("\r\n", "\n");

    /// <summary>
    /// Snapshot of the expected prompt. Update when schemas change.
    /// </summary>
    private const string Expected =
"""

SUPPORTED ENTITIES
- "Customer" → call tool "query_customer"
- "Product" → call tool "query_product"
- "InventoryEvent" → call tool "query_inventoryevent"
- "Financial" → call tool "query_financial"
- "Sale" → call tool "query_sale"
- "SalesReturn" → call tool "query_salesreturn"

ENTITY: "Customer"

| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |
|-------|------|------------|------------|-----------|-----------|--------------|
| Active | bool | yes (eq, ne) | yes | no | yes | no |
| PaymentReliability | float | yes (eq, ge, le) | yes | yes | no | yes (avg) |
| CustomerType | enum | yes (eq, ne) | yes | no | yes | no |
| Name | string | yes (eq, ne) | yes | yes | no | no |
| Family | string | yes (eq, ne) | yes | yes | no | no |
| Code | string | yes (eq, ne) | yes | yes | no | no |
| Phone | string | yes (eq, ne) | yes | yes | no | no |
| Mobile | string | yes (eq, ne) | yes | yes | no | no |
| Address | string | yes (eq) | yes | yes | no | no |
| BirthDate | datetime | yes (ge, le) | yes | yes | no | no |
| City | string | yes (eq, ne) | yes | yes | yes | no |
| Province | string | yes (eq, ne) | yes | yes | yes | no |

Customer.CustomerType allowed values: None, Bedehkaran, Bestankar, Personnel, Ranandeh, Bazaryab, Sherka, MoshtarianKhanegi, JariSherkathaVaAshkhas, TarahVaEditor

ENTITY: "Product"

| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |
|-------|------|------------|------------|-----------|-----------|--------------|
| Type | enum | yes (eq, ne) | yes | no | yes | no |
| ProductCode | string | yes (eq, ne) | yes | yes | no | no |
| ProductDescription | string | yes (eq, ne) | yes | yes | no | no |
| DetailedType | string | yes (eq, ne) | yes | yes | yes | no |
| CreatedAt | datetime | yes (ge, le) | yes | yes | yes | no |

Product.Type allowed values: None, Carpet, RawMaterials, Rug

ENTITY: "InventoryEvent"

| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |
|-------|------|------------|------------|-----------|-----------|--------------|
| Type | enum | yes (eq, ne) | yes | no | yes | no |
| Date | datetime | yes (ge, le) | yes | yes | yes | no |
| Quantity | float | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| UnitPrice | int64 | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| TotalPrice | int64 | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| FactorNumber | int32 | yes (eq, ne) | yes | yes | no | no |
| Description | string | yes (eq) | yes | yes | no | no |

InventoryEvent.Type allowed values: None, Receipt, Issue, SalesOrConsumptionRequest, PurchaseOrProductionRequest, ProformaInvoice, SalesInvoice

ENTITY: "Financial"

| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |
|-------|------|------------|------------|-----------|-----------|--------------|
| Code | string | yes (eq, ne) | yes | yes | no | no |
| Name | string | yes (eq, ne) | yes | yes | yes | no |
| Type | string | yes (eq, ne) | yes | yes | yes | no |
| Debit | int64 | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| Credit | int64 | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |

ENTITY: "Sale"

| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |
|-------|------|------------|------------|-----------|-----------|--------------|
| DocNumber | int32 | yes (eq, ne, ge, le) | yes | yes | no | no |
| Date | datetime | yes (ge, le) | yes | yes | yes | no |
| Price | int64 | yes (eq, ne, ge, le) | yes | yes | no | yes (sum, avg) |
| Quantity | float | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| DeliveredQuantity | float | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| ProductCode | string | yes (eq, ne) | yes | yes | yes | no |
| ProductDescription | string | yes (eq, ne) | yes | yes | no | no |
| ProductDetailedType | string | yes (eq, ne) | yes | yes | yes | no |
| CustomerPaymentReliability | float | yes (ge, le) | yes | yes | no | yes (avg) |
| CustomerName | string | yes (eq, ne) | yes | yes | no | no |
| CustomerFamily | string | yes (eq, ne) | yes | yes | no | no |
| CustomerMobile | string | yes (eq, ne) | yes | yes | no | no |
| CustomerAddress | string | yes (eq) | yes | yes | no | no |
| CustomerCity | string | yes (eq, ne) | yes | yes | yes | no |
| CustomerProvince | string | yes (eq, ne) | yes | yes | yes | no |

ENTITY: "SalesReturn"

| Field | Type | Filterable | Selectable | Orderable | Groupable | Aggregatable |
|-------|------|------------|------------|-----------|-----------|--------------|
| DocNumber | int32 | yes (eq, ne, ge, le) | yes | yes | no | no |
| Date | datetime | yes (ge, le) | yes | yes | yes | no |
| Price | int64 | yes (eq, ne, ge, le) | yes | yes | no | yes (sum, avg) |
| Quantity | float | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| DeliveredQuantity | float | yes (eq, ge, le) | yes | yes | no | yes (sum, avg) |
| ProductCode | string | yes (eq, ne) | yes | yes | yes | no |
| ProductDescription | string | yes (eq, ne) | yes | yes | no | no |
| ProductDetailedType | string | yes (eq, ne) | yes | yes | yes | no |
| CustomerName | string | yes (eq, ne) | yes | yes | no | no |
| CustomerFamily | string | yes (eq, ne) | yes | yes | no | no |
| CustomerCity | string | yes (eq, ne) | yes | yes | yes | no |
| CustomerProvince | string | yes (eq, ne) | yes | yes | yes | no |


""";

    private sealed class SnapshotTool(string toolName, string description, EntitySchema schema) : IQueryableTool
    {
        public string ToolName { get; } = toolName;
        public string Description { get; } = description;
        public EntitySchema Schema { get; } = schema;
        public Task<object> ExecuteAsync(AiQueryRequest r, CancellationToken c)
            => Task.FromResult<object>(new { });
    }
}
