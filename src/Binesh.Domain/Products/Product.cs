namespace Binesh.Domain.Products;

public sealed class Product
{
    public Guid Id { get; private set; }
    public ProductType Type { get; private set; }

    /// <summary>Subtype string from the upstream ETL (e.g. specific carpet line).</summary>
    public string DetailedType { get; private set; } = default!;

    /// <summary>Stock-keeping code (Kala Code).</summary>
    public string ProductCode { get; private set; } = default!;

    public string ProductDescription { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    public List<InventoryEvent> Events { get; private set; } = [];

    // EF Core
    private Product() { }

    public static Product Create(
        ProductType type,
        string productCode,
        string productDescription,
        string detailedType)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new ArgumentException("ProductCode is required.", nameof(productCode));
        }
        if (string.IsNullOrWhiteSpace(productDescription))
        {
            throw new ArgumentException("ProductDescription is required.", nameof(productDescription));
        }

        return new Product
        {
            Id = Guid.NewGuid(),
            Type = type,
            ProductCode = productCode.Trim(),
            ProductDescription = productDescription.Trim(),
            DetailedType = (detailedType ?? string.Empty).Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(ProductType? type, string? productCode, string? productDescription, string? detailedType)
    {
        if (type is not null) { Type = type.Value; }
        if (!string.IsNullOrWhiteSpace(productCode)) { ProductCode = productCode.Trim(); }
        if (!string.IsNullOrWhiteSpace(productDescription)) { ProductDescription = productDescription.Trim(); }
        if (detailedType is not null) { DetailedType = detailedType.Trim(); }
    }
}
