using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.Panel.GetRegionalSales;

/// <summary>
/// Port of legacy <c>SalesApiController.GetProvinceategorizdeSalesAsync</c>.
/// In the legacy code the entire body was commented out and it returned an
/// empty <see cref="RegionalSalesDto"/> — reproduced here verbatim for parity
/// (the intended per-city/growth logic will be defined later).
/// </summary>
public sealed record GetRegionalSalesQuery(SalesPageRequestDto Request)
    : IRequest<ApiResponse<RegionalSalesDto>>;
