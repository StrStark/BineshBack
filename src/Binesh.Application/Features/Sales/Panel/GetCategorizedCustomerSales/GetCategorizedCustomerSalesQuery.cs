using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.Panel.GetCategorizedCustomerSales;

/// <summary>
/// Port of legacy <c>SalesApiController.GetCustomercategorizedSalesAsync</c>.
/// Counts sales grouped by customer type and time bucket over the requested range.
/// </summary>
public sealed record GetCategorizedCustomerSalesQuery(SalesPageRequestDto Request)
    : IRequest<ApiResponse<CategorizedSales>>;
