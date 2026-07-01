using Binesh.Ai.QueryEngine;
using Binesh.Domain.Products;

namespace Binesh.Ai.Schemas;

/// <summary>
/// Field metadata for <see cref="Product"/>. Adds <c>Type</c> as an
/// enum-typed field (driven by <see cref="ProductType"/>) — the old schema
/// only carried free-text product subtype.
/// </summary>
public static class ProductSchema
{
    public static EntitySchema Build() => new()
    {
        Name = "Product",
        EntityType = typeof(Product),
        Fields =
        [
            new FieldDescriptor
            {
                Name = "Type",
                Type = FieldType.Enum,
                Selector = (Product p) => p.Type,
                AllowedOperators = ["eq", "ne"],
                Orderable = false,
                Selectable = true,
                Groupable = true,
                Aggregatable = false,
                AllowedValues = Enum.GetNames<ProductType>(),
            },
            new FieldDescriptor
            {
                Name = "ProductCode",
                Type = FieldType.String,
                Selector = (Product p) => p.ProductCode,
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "ProductDescription",
                Type = FieldType.String,
                Selector = (Product p) => p.ProductDescription,
                AllowedOperators = ["eq", "ne"],
            },
            new FieldDescriptor
            {
                Name = "DetailedType",
                Type = FieldType.String,
                Selector = (Product p) => p.DetailedType,
                AllowedOperators = ["eq", "ne"],
                Groupable = true,
            },
            new FieldDescriptor
            {
                Name = "CreatedAt",
                Type = FieldType.DateTime,
                Selector = (Product p) => p.CreatedAt,
                AllowedOperators = ["ge", "le"],
                Groupable = true,
            },
        ],
    };
}
