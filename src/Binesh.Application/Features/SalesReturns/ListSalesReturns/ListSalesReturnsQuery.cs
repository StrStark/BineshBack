using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;

namespace Binesh.Application.Features.SalesReturns.ListSalesReturns;

public sealed record ListSalesReturnsQuery(
    DateOnly? From,
    DateOnly? To,
    Guid? CustomerId,
    Guid? ProductId,
    string? Search,
    int Page,
    int PageSize)
    : IRequest<ListSalesReturnsResponse>;

public sealed record ListSalesReturnsResponse(
    IReadOnlyList<SalesReturnDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
