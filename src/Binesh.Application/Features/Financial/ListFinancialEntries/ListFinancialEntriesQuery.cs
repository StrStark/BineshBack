using Binesh.Application.Features.Financial.Shared;
using MediatR;

namespace Binesh.Application.Features.Financial.ListFinancialEntries;

public sealed record ListFinancialEntriesQuery(
    string? Search,
    string? Type,
    int Page,
    int PageSize)
    : IRequest<ListFinancialEntriesResponse>;

public sealed record ListFinancialEntriesResponse(
    IReadOnlyList<FinancialEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
