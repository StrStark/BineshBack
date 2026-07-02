namespace Binesh.Domain.Customers;

/// <summary>
/// A business counterparty (debtor, creditor, employee, partner, etc.). The
/// classification is given by <see cref="Type"/>; see <see cref="CustomerType"/>
/// for the Iranian accounting categories.
///
/// One Customer owns exactly one <see cref="Person"/> record (1-to-1, cascade-deleted).
/// </summary>
public sealed class Customer
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }

    public CustomerType Type { get; private set; }

    public bool Active { get; private set; }

    /// <summary>
    /// Soft credit score — 0.0 (never pays) to 1.0 (always pays on time).
    /// Filled in by the user or a future scoring rule, not by the system.
    /// </summary>
    public float PaymentReliability { get; private set; }

    public Guid PersonId { get; private set; }
    public Person Person { get; private set; } = default!;

    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core
    private Customer() { }

    public static Customer Create(
        Guid companyId,
        CustomerType type,
        bool active,
        float paymentReliability,
        Person person)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
        }
        if (paymentReliability is < 0f or > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(paymentReliability),
                "PaymentReliability must be between 0 and 1.");
        }

        return new Customer
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Type = type,
            Active = active,
            PaymentReliability = paymentReliability,
            Person = person,
            PersonId = person.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void Update(CustomerType? type, bool? active, float? paymentReliability)
    {
        if (type is not null) { Type = type.Value; }
        if (active is not null) { Active = active.Value; }
        if (paymentReliability is not null)
        {
            if (paymentReliability is < 0f or > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(paymentReliability),
                    "PaymentReliability must be between 0 and 1.");
            }
            PaymentReliability = paymentReliability.Value;
        }
    }
}
