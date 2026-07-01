using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.Panel.GetTopSellingPanelProducts;

/// <summary>
/// Port of legacy <c>SalesApiController.GetTopSellingProductsAsync</c>. Top 5
/// products by delivered quantity in the current window, with growth vs the
/// preceding window of equal length.
/// </summary>
public sealed record GetTopSellingPanelProductsQuery(SalesPageRequestDto Request)
    : IRequest<ApiResponse<TopSellingProductsDto>>;
