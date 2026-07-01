using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.Panel.GetPanelSummary;

/// <summary>
/// Port of legacy <c>SalesApiController.GetSalesSummaryAsync</c>.
/// POST body → sold-items-by-category + total/return cards with growth vs the
/// immediately-preceding window of equal length.
/// </summary>
public sealed record GetPanelSummaryQuery(SalesPageRequestDto Request)
    : IRequest<ApiResponse<SalesSummaryDto>>;
