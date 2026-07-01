using Binesh.Application.Features.Sales.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.ListSales;

/// <summary>
/// Paginated sale list with optional filters.
/// </summary>
public sealed record ListSalesQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? CustomerId,
    Guid? ProductId,
    string? Search,
    int Page,
    int PageSize)
    : IRequest<ListSalesResponse>;

public sealed record ListSalesResponse(
    IReadOnlyList<SaleDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
