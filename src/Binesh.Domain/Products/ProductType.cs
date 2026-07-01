using System.Text.Json.Serialization;

namespace Binesh.Domain.Products;

[JsonConverter(typeof(JsonStringEnumConverter<ProductType>))]
public enum ProductType
{
    None = 0,
    Carpet = 1,
    RawMaterials = 2,
    Rug = 4,
}
