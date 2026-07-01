namespace Binesh.Domain.Customers;

/// <summary>
/// Geographic location reused across persons. The tuple
/// (<see cref="Country"/>, <see cref="Province"/>, <see cref="City"/>) is unique
/// — see the configuration in <c>Binesh.Infrastructure.Persistence.Configurations.RegionConfiguration</c>.
/// </summary>
public sealed class Region
{
    public Guid Id { get; private set; }

    public string? Country { get; private set; }
    public string? Province { get; private set; }
    public string? City { get; private set; }

    // EF Core
    private Region() { }

    public static Region Create(string? country, string? province, string? city)
    {
        return new Region
        {
            Id = Guid.NewGuid(),
            Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim(),
            Province = string.IsNullOrWhiteSpace(province) ? null : province.Trim(),
            City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
        };
    }

    public string Format() => string.Join(" - ",
        new[] { Country, Province, City }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
