namespace Binesh.Domain.Customers;

/// <summary>
/// Contact details for a customer. One person, one customer record — Person is
/// owned by Customer (no shared people across customers yet).
///
/// Note: <c>Mobile</c> is renamed from the old <c>PhoneNumber</c> because
/// "PhoneNumber" already means the login phone on <see cref="Binesh.Domain.Identity.User"/>
/// — two different things in the codebase caused real confusion.
/// </summary>
public sealed class Person
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = default!;
    public string? Family { get; private set; }

    /// <summary>External / accounting code (Kod-e Hesab).</summary>
    public string? Code { get; private set; }

    /// <summary>Landline.</summary>
    public string? Phone { get; private set; }
    public string? Fax { get; private set; }

    /// <summary>Mobile number for SMS / contact. Was <c>PhoneNumber</c> in the old schema.</summary>
    public string? Mobile { get; private set; }

    /// <summary>Plot / plate (Pelak) — Iranian street-address subdivision.</summary>
    public string? Pelak { get; private set; }

    public string? Address { get; private set; }
    public DateTimeOffset? BirthDate { get; private set; }

    public Guid? RegionId { get; private set; }
    public Region? Region { get; private set; }

    // EF Core
    private Person() { }

    public static Person Create(
        string name,
        string? family,
        string? code,
        string? phone,
        string? mobile,
        string? fax,
        string? pelak,
        string? address,
        DateTimeOffset? birthDate,
        Region? region)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Person
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Family = string.IsNullOrWhiteSpace(family) ? null : family.Trim(),
            Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim(),
            Phone = phone,
            Mobile = mobile,
            Fax = fax,
            Pelak = pelak,
            Address = address,
            BirthDate = birthDate,
            Region = region,
            RegionId = region?.Id,
        };
    }

    public void Update(
        string? name,
        string? family,
        string? code,
        string? phone,
        string? mobile,
        string? fax,
        string? pelak,
        string? address,
        DateTimeOffset? birthDate,
        Region? region)
    {
        if (name is not null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name cannot be blank.", nameof(name));
            }
            Name = name.Trim();
        }

        if (family is not null) { Family = family; }
        if (code is not null) { Code = code; }
        if (phone is not null) { Phone = phone; }
        if (mobile is not null) { Mobile = mobile; }
        if (fax is not null) { Fax = fax; }
        if (pelak is not null) { Pelak = pelak; }
        if (address is not null) { Address = address; }
        if (birthDate is not null) { BirthDate = birthDate; }

        if (region is not null)
        {
            Region = region;
            RegionId = region.Id;
        }
    }

    public string FullName =>
        string.IsNullOrWhiteSpace(Family) ? Name : $"{Name} {Family}";
}
