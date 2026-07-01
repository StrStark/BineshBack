using Binesh.Domain.Customers;
using Binesh.Domain.Products;

namespace Binesh.Domain.Sales;

/// <summary>
/// A single sales-return line — one product returned by one counterparty
/// on one date. Mirror of <see cref="Sale"/>; kept as a separate aggregate
/// rather than a flag on Sale so reporting can join/exclude returns cleanly
/// and so the upstream ETL can drive them independently.
/// </summary>
public sealed class SalesReturn
{
    public Guid Id { get; private set; }
    public DateTime Date { get; private set; }

    /// <summary>Total line price in Iranian Rial. Bigint.</summary>
    public long Price { get; private set; }

    /// <summary>Quantity on the return line.</summary>
    public float Quantity { get; private set; }

    /// <summary>Quantity actually returned to inventory (may be &lt; <see cref="Quantity"/>).</summary>
    public float DeliveredQuantity { get; private set; }

    /// <summary>Document / invoice number from the upstream ETL source.</summary>
    public int DocNumber { get; private set; }

    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = default!;

    public Guid CounterpartyId { get; private set; }
    public Customer Counterparty { get; private set; } = default!;

    // EF Core
    private SalesReturn() { }

    public static SalesReturn Create(
        DateTime date,
        long price,
        float quantity,
        float deliveredQuantity,
        int docNumber,
        Guid productId,
        Guid counterpartyId)
    {
        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }
        if (deliveredQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deliveredQuantity), "DeliveredQuantity cannot be negative.");
        }
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("ProductId is required.", nameof(productId));
        }
        if (counterpartyId == Guid.Empty)
        {
            throw new ArgumentException("CounterpartyId is required.", nameof(counterpartyId));
        }

        return new SalesReturn
        {
            Id = Guid.NewGuid(),
            Date = EnsureUtc(date),
            Price = price,
            Quantity = quantity,
            DeliveredQuantity = deliveredQuantity,
            DocNumber = docNumber,
            ProductId = productId,
            CounterpartyId = counterpartyId,
        };
    }

    public void Update(
        DateTime? date,
        long? price,
        float? quantity,
        float? deliveredQuantity,
        int? docNumber,
        Guid? productId,
        Guid? counterpartyId)
    {
        if (date is { } d)
        {
            Date = EnsureUtc(d);
        }
        if (price is { } p)
        {
            if (p < 0) { throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative."); }
            Price = p;
        }
        if (quantity is { } q)
        {
            if (q <= 0) { throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive."); }
            Quantity = q;
        }
        if (deliveredQuantity is { } dq)
        {
            if (dq < 0) { throw new ArgumentOutOfRangeException(nameof(deliveredQuantity), "DeliveredQuantity cannot be negative."); }
            DeliveredQuantity = dq;
        }
        if (docNumber is { } dn)
        {
            DocNumber = dn;
        }
        if (productId is { } pid)
        {
            if (pid == Guid.Empty) { throw new ArgumentException("ProductId is required.", nameof(productId)); }
            ProductId = pid;
        }
        if (counterpartyId is { } cid)
        {
            if (cid == Guid.Empty) { throw new ArgumentException("CounterpartyId is required.", nameof(counterpartyId)); }
            CounterpartyId = cid;
        }
    }

    private static DateTime EnsureUtc(DateTime date) =>
        date.Kind == DateTimeKind.Utc
            ? date
            : DateTime.SpecifyKind(date, DateTimeKind.Utc);
}
