using Binesh.Application.Features.Customers.Shared;
using Binesh.Domain.Customers;
using MediatR;

namespace Binesh.Application.Features.Customers.ListCustomers;

/// <summary>
/// Paginated customer list with optional filters.
/// </summary>
public sealed record ListCustomersQuery(
    string? Search,
    CustomerType? Type,
    bool? Active,
    int Page,
    int PageSize)
    : IRequest<ListCustomersResponse>;

public sealed record ListCustomersResponse(
    IReadOnlyList<CustomerDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
