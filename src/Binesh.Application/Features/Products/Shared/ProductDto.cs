using Binesh.Domain.Products;

namespace Binesh.Application.Features.Products.Shared;

public sealed record ProductDto(
    Guid Id,
    ProductType Type,
    string ProductCode,
    string ProductDescription,
    string DetailedType,
    DateTimeOffset CreatedAt);

/// <summary>Product summary with computed inventory aggregates for the panel list view.</summary>
public sealed record ProductWithStatsDto(
    Guid Id,
    ProductType Type,
    string ProductCode,
    string ProductDescription,
    string DetailedType,
    long TotalUnitPriceSum,
    long TotalRevenueSum,
    int EventCount);

public sealed record InventoryEventDto(
    Guid Id,
    Guid ProductId,
    InventoryEventType Type,
    DateTime Date,
    float Quantity,
    long UnitPrice,
    long TotalPrice,
    int FactorNumber,
    string? Value1,
    string? Value2,
    string? Value3,
    string? Description);

public static class ProductProjection
{
    public static ProductDto ToDto(Product p) => new(
        p.Id, p.Type, p.ProductCode, p.ProductDescription, p.DetailedType, p.CreatedAt);

    public static InventoryEventDto ToDto(InventoryEvent e) => new(
        e.Id, e.ProductId, e.Type, e.Date, e.Quantity, e.UnitPrice, e.TotalPrice,
        e.FactorNumber, e.Value1, e.Value2, e.Value3, e.Description);
}
