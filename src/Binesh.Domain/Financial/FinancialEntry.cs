namespace Binesh.Domain.Financial;

/// <summary>
/// One chart-of-accounts row. Renamed from the old <c>FinantialModel</c>
/// (typo fixed: Finantial → Financial). Each entry carries the running
/// debit (<see cref="Debit"/>) and credit (<see cref="Credit"/>) balances
/// in Iranian Rial. The old Persian field names <c>Bedehkar</c> (debit)
/// and <c>Bestankar</c> (credit) are translated to their English
/// accounting equivalents on the new schema.
/// </summary>
public sealed class FinancialEntry
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }

    /// <summary>Hierarchical account code (Kol Code in old code).</summary>
    public string Code { get; private set; } = default!;

    public string Name { get; private set; } = default!;

    /// <summary>Account type / category — free-text in the legacy schema.</summary>
    public string Type { get; private set; } = default!;

    /// <summary>بدهکار — debit balance. Rial, bigint.</summary>
    public long Debit { get; private set; }

    /// <summary>بستانکار — credit balance. Rial, bigint.</summary>
    public long Credit { get; private set; }

    // EF Core
    private FinancialEntry() { }

    public static FinancialEntry Create(Guid companyId, string code, string name, string type, long debit, long credit)
    {
        if (companyId == Guid.Empty)
        {
            throw new ArgumentException("CompanyId is required.", nameof(companyId));
        }
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code is required.", nameof(code));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Type is required.", nameof(type));
        }
        if (debit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(debit), "Debit cannot be negative.");
        }
        if (credit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(credit), "Credit cannot be negative.");
        }

        return new FinancialEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Code = code.Trim(),
            Name = name.Trim(),
            Type = type.Trim(),
            Debit = debit,
            Credit = credit,
        };
    }

    public void Update(string? code, string? name, string? type, long? debit, long? credit)
    {
        if (code is not null)
        {
            if (string.IsNullOrWhiteSpace(code)) { throw new ArgumentException("Code cannot be blank.", nameof(code)); }
            Code = code.Trim();
        }
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name)) { throw new ArgumentException("Name cannot be blank.", nameof(name)); }
            Name = name.Trim();
        }
        if (type is not null)
        {
            if (string.IsNullOrWhiteSpace(type)) { throw new ArgumentException("Type cannot be blank.", nameof(type)); }
            Type = type.Trim();
        }
        if (debit is { } d)
        {
            if (d < 0) { throw new ArgumentOutOfRangeException(nameof(debit), "Debit cannot be negative."); }
            Debit = d;
        }
        if (credit is { } c)
        {
            if (c < 0) { throw new ArgumentOutOfRangeException(nameof(credit), "Credit cannot be negative."); }
            Credit = c;
        }
    }
}
