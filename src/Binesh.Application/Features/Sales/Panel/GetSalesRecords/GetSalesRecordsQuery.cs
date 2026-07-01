using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.Panel.GetSalesRecords;

/// <summary>
/// Port of legacy <c>SalesApiController.GetSalesRecords</c>. Paginated, searchable
/// sales rows for the panel table. Filtering + paging run in SQL.
/// </summary>
public sealed record GetSalesRecordsQuery(SalesPageRequestPaginatedDto Request)
    : IRequest<ApiResponse<PagedResult<SalesRecordsDto>>>;
