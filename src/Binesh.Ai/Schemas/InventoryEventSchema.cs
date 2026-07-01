using Binesh.Ai.QueryEngine;
using Binesh.Domain.Products;

namespace Binesh.Ai.Schemas;

/// <summary>
/// Field metadata for <see cref="InventoryEvent"/>. Field renames from old:
/// FactorNumber → Quantity (now means the moved qty; the legacy "factor
/// number" meaning lives on as <c>FactorNumber</c> = invoice number),
/// Fee → UnitPrice, Price → TotalPrice, Desc → Description. See Round 8
/// CHANGES.md.
/// </summary>
public static class InventoryEventSchema
{
    public static EntitySchema Build() => new()
    {
        Name = "InventoryEvent",
        EntityType = typeof(InventoryEvent),
        Fields =
        [
            new FieldDescriptor
            {
                Name = "Type",
                Type = FieldType.Enum,
                Selector = (InventoryEvent e) => e.Type,
                AllowedOperators = ["eq", "ne"],
                Orderable = false,
                Selectable = true,
                Groupable = true,
                AllowedValues = Enum.GetNames<InventoryEventType>(),
            },
            new FieldDescriptor
            {
                Name = "Date",
                Type = FieldType.DateTime,
                Selector = (InventoryEvent e) => e.Date,
                AllowedOperators = ["ge", "le"],
                Orderable = true,
                Selectable = true,
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "Quantity",
                Type = FieldType.Float,
                Selector = (InventoryEvent e) => e.Quantity,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "UnitPrice",
                Type = FieldType.Int64,
                Selector = (InventoryEvent e) => e.UnitPrice,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "TotalPrice",
                Type = FieldType.Int64,
                Selector = (InventoryEvent e) => e.TotalPrice,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "FactorNumber",  // now means invoice number on the new schema
                Type = FieldType.Int32,
                Selector = (InventoryEvent e) => e.FactorNumber,
                AllowedOperators = ["eq", "ne"],
                Orderable = true,
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Description",   // renamed from old "Desc"
                Type = FieldType.String,
                Selector = (InventoryEvent e) => e.Description,
                AllowedOperators = ["eq"],
                Selectable = true,
            },
        ],
    };
}
