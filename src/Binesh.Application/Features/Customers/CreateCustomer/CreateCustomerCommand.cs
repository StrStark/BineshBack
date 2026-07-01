using Binesh.Application.Features.Customers.Shared;
using Binesh.Domain.Customers;
using MediatR;

namespace Binesh.Application.Features.Customers.CreateCustomer;

public sealed record CreateCustomerCommand(
    CustomerType Type,
    bool Active,
    float PaymentReliability,
    PersonInput Person)
    : IRequest<CustomerDto>;

/// <summary>
/// Nested person input. Region is looked up by (Country, Province, City) and
/// created if absent.
/// </summary>
public sealed record PersonInput(
    string Name,
    string? Family,
    string? Code,
    string? Phone,
    string? Mobile,
    string? Fax,
    string? Pelak,
    string? Address,
    DateTimeOffset? BirthDate,
    RegionInput? Region);

public sealed record RegionInput(
    string? Country,
    string? Province,
    string? City);
