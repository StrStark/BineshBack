namespace Binesh.Domain.Products;

/// <summary>
/// A single inventory ledger entry for a product (receipt, issue, invoice, etc.).
/// Renamed from the old plural <c>InventoryEvents</c>. Field renames for
/// clarity:
///   FactorNumber → Quantity
///   Fee          → UnitPrice
///   Price        → TotalPrice
///   Desc         → Description
/// Value1/Value2/Value3 are kept verbatim because they're free-form metadata
/// from the upstream ETL with no fixed meaning yet.
/// </summary>
public sealed class InventoryEvent
{
    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }

    public InventoryEventType Type { get; private set; }
    public DateTime Date { get; private set; }

    public float Quantity { get; private set; }

    /// <summary>Per-unit price in IRR.</summary>
    public long UnitPrice { get; private set; }

    /// <summary>Total amount for the line in IRR.</summary>
    public long TotalPrice { get; private set; }

    public int FactorNumber { get; private set; }

    public string? Value1 { get; private set; }
    public string? Value2 { get; private set; }
    public string? Value3 { get; private set; }
    public string? Description { get; private set; }

    // EF Core
    private InventoryEvent() { }

    public static InventoryEvent Create(
        Guid productId,
        InventoryEventType type,
        DateTime date,
        float quantity,
        long unitPrice,
        long totalPrice,
        int factorNumber,
        string? value1 = null,
        string? value2 = null,
        string? value3 = null,
        string? description = null)
    {
        if (quantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        }
        if (unitPrice < 0 || totalPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(unitPrice), "Prices cannot be negative.");
        }

        return new InventoryEvent
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Type = type,
            Date = EnsureUtc(date),
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = totalPrice,
            FactorNumber = factorNumber,
            Value1 = value1,
            Value2 = value2,
            Value3 = value3,
            Description = description,
        };
    }

    private static DateTime EnsureUtc(DateTime date) =>
        date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);
}
