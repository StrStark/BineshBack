namespace Binesh.Domain.Products;

public sealed class Product
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
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
        Guid companyId,
        ProductType type,
        string productCode,
        string productDescription,
        string detailedType)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
        }
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
            CompanyId = companyId,
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
