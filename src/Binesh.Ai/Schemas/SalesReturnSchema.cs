using Binesh.Ai.QueryEngine;
using Binesh.Domain.Sales;

namespace Binesh.Ai.Schemas;

/// <summary>
/// Field metadata for <see cref="SalesReturn"/>. Identical shape to
/// <see cref="SaleSchema"/> — the old <c>State</c> (RequestState) field is
/// dropped per the Round 10 decision (unused on the panel side; ETL never
/// drove it consistently).
/// </summary>
public static class SalesReturnSchema
{
    public static EntitySchema Build() => new()
    {
        Name = "SalesReturn",
        EntityType = typeof(SalesReturn),
        Fields =
        [
            new FieldDescriptor
            {
                Name = "DocNumber",
                Type = FieldType.Int32,
                Selector = (SalesReturn r) => r.DocNumber,
                AllowedOperators = ["eq", "ne", "ge", "le"],
                Orderable = true,
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Date",
                Type = FieldType.DateTime,
                Selector = (SalesReturn r) => r.Date,
                AllowedOperators = ["ge", "le"],
                Orderable = true,
                Groupable = true,
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Price",
                Type = FieldType.Int64,
                Selector = (SalesReturn r) => r.Price,
                AllowedOperators = ["eq", "ne", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "Quantity",
                Type = FieldType.Float,
                Selector = (SalesReturn r) => r.Quantity,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "DeliveredQuantity",
                Type = FieldType.Float,
                Selector = (SalesReturn r) => r.DeliveredQuantity,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },
            new FieldDescriptor
            {
                Name = "ProductCode",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Product.ProductCode,
                RequiredIncludes = ["Product"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "ProductDescription",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Product.ProductDescription,
                RequiredIncludes = ["Product"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "ProductDetailedType",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Product.DetailedType,
                RequiredIncludes = ["Product"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "CustomerName",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Counterparty.Person.Name,
                RequiredIncludes = ["Counterparty.Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "CustomerFamily",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Counterparty.Person.Family,
                RequiredIncludes = ["Counterparty.Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "CustomerCity",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Counterparty.Person.Region!.City,
                RequiredIncludes = ["Counterparty.Person.Region"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "CustomerProvince",
                Type = FieldType.String,
                Selector = (SalesReturn r) => r.Counterparty.Person.Region!.Province,
                RequiredIncludes = ["Counterparty.Person.Region"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
        ],
    };
}
