using Binesh.Ai.QueryEngine;
using Binesh.Domain.Sales;

namespace Binesh.Ai.Schemas;

/// <summary>
/// Field metadata for <see cref="Sale"/>. Customer-side fields use the new
/// <c>Person.Mobile</c> (was <c>PhoneNumber</c>) name. Navigation depth:
/// <code>
/// Sale
/// ├── Product                                 (Includes: ["Product"])
/// └── Counterparty                            (Includes: ["Counterparty"])
///     └── Person                              (Includes: ["Counterparty.Person"])
///         └── Region                          (Includes: ["Counterparty.Person.Region"])
/// </code>
/// </summary>
public static class SaleSchema
{
    public static EntitySchema Build() => new()
    {
        Name = "Sale",
        EntityType = typeof(Sale),
        Fields =
        [
            // ── Flat Sale fields ────────────────────────────────────────────
            new FieldDescriptor
            {
                Name = "DocNumber",
                Type = FieldType.Int32,
                Selector = (Sale s) => s.DocNumber,
                AllowedOperators = ["eq", "ne", "ge", "le"],
                Orderable = true,
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Date",
                Type = FieldType.DateTime,
                Selector = (Sale s) => s.Date,
                AllowedOperators = ["ge", "le"],
                Orderable = true,
                Groupable = true,
                Selectable = true,
            },
            new FieldDescriptor
            {
                Name = "Price",
                Type = FieldType.Int64,
                Selector = (Sale s) => s.Price,
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
                Selector = (Sale s) => s.Quantity,
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
                Selector = (Sale s) => s.DeliveredQuantity,
                AllowedOperators = ["eq", "ge", "le"],
                Orderable = true,
                Selectable = true,
                Aggregatable = true,
                AllowedAggregates = ["sum", "avg"],
            },

            // ── Product navigation ──────────────────────────────────────────
            new FieldDescriptor
            {
                Name = "ProductCode",
                Type = FieldType.String,
                Selector = (Sale s) => s.Product.ProductCode,
                RequiredIncludes = ["Product"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "ProductDescription",
                Type = FieldType.String,
                Selector = (Sale s) => s.Product.ProductDescription,
                RequiredIncludes = ["Product"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "ProductDetailedType",
                Type = FieldType.String,
                Selector = (Sale s) => s.Product.DetailedType,
                RequiredIncludes = ["Product"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },

            // ── Counterparty (Customer) navigation ──────────────────────────
            new FieldDescriptor
            {
                Name = "CustomerPaymentReliability",
                Type = FieldType.Float,
                Selector = (Sale s) => s.Counterparty.PaymentReliability,
                RequiredIncludes = ["Counterparty"],
                AllowedOperators = ["ge", "le"],
                Aggregatable = true,
                AllowedAggregates = ["avg"],
            },

            // ── Person (Counterparty.Person) navigation ─────────────────────
            new FieldDescriptor
            {
                Name = "CustomerName",
                Type = FieldType.String,
                Selector = (Sale s) => s.Counterparty.Person.Name,
                RequiredIncludes = ["Counterparty.Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "CustomerFamily",
                Type = FieldType.String,
                Selector = (Sale s) => s.Counterparty.Person.Family,
                RequiredIncludes = ["Counterparty.Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "CustomerMobile",   // was CustomerPhoneNumber
                Type = FieldType.String,
                Selector = (Sale s) => s.Counterparty.Person.Mobile,
                RequiredIncludes = ["Counterparty.Person"],
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "CustomerAddress",
                Type = FieldType.String,
                Selector = (Sale s) => s.Counterparty.Person.Address,
                RequiredIncludes = ["Counterparty.Person"],
                AllowedOperators = ["eq"],
            },

            // ── Region (Counterparty.Person.Region) navigation ──────────────
            new FieldDescriptor
            {
                Name = "CustomerCity",
                Type = FieldType.String,
                Selector = (Sale s) => s.Counterparty.Person.Region!.City,
                RequiredIncludes = ["Counterparty.Person.Region"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "CustomerProvince",
                Type = FieldType.String,
                Selector = (Sale s) => s.Counterparty.Person.Region!.Province,
                RequiredIncludes = ["Counterparty.Person.Region"],
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
        ],
    };
}
