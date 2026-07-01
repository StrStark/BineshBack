using System.Net;
using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.Panel.GetRegionalSales;

public sealed class GetRegionalSalesHandler
    : IRequestHandler<GetRegionalSalesQuery, ApiResponse<RegionalSalesDto>>
{
    public Task<ApiResponse<RegionalSalesDto>> Handle(
        GetRegionalSalesQuery request, CancellationToken cancellationToken)
    {
        // Legacy parity: the real aggregation was commented out and the endpoint
        // returned an empty payload. Preserved exactly. When the intended logic
        // is defined we implement it here (Sale → Counterparty → Person → Region).
        var response = new RegionalSalesDto();

        return Task.FromResult(ApiResponse<RegionalSalesDto>.Success(
            "Sales Fetched successfully", HttpStatusCode.OK, response));
    }
}
