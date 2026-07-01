using Binesh.Domain.Customers;

namespace Binesh.Application.Features.Customers.Shared;

public sealed record CustomerDto(
    Guid Id,
    CustomerType Type,
    bool Active,
    float PaymentReliability,
    PersonDto Person,
    DateTimeOffset CreatedAt);

public static class CustomerProjection
{
    public static CustomerDto ToDto(Customer customer) => new(
        customer.Id,
        customer.Type,
        customer.Active,
        customer.PaymentReliability,
        ToDto(customer.Person),
        customer.CreatedAt);

    public static PersonDto ToDto(Person person) => new(
        person.Id,
        person.Name,
        person.Family,
        person.Code,
        person.Phone,
        person.Mobile,
        person.Fax,
        person.Pelak,
        person.Address,
        person.BirthDate,
        person.Region is null ? null : ToDto(person.Region));

    public static RegionDto ToDto(Region region) => new(
        region.Id,
        region.Country,
        region.Province,
        region.City);
}
