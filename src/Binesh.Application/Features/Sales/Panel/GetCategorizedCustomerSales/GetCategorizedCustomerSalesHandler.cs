using System.Net;
using Binesh.Application.Abstractions;
using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.Panel.GetCategorizedCustomerSales;

public sealed class GetCategorizedCustomerSalesHandler(IBineshDbContext db)
    : IRequestHandler<GetCategorizedCustomerSalesQuery, ApiResponse<CategorizedSales>>
{
    public async Task<ApiResponse<CategorizedSales>> Handle(
        GetCategorizedCustomerSalesQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var start = PanelMath.AsUtc(req.DateFilter.StartTime);
        var end = PanelMath.AsUtc(req.DateFilter.EndTime);
        var timeFrameUnit = req.DateFilter.TimeFrameUnit;

        // Pull the minimal columns; the time-bucket grouping is done in memory
        // because GetTimeFrameStart is not SQL-translatable.
        var data = await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= start && s.Date <= end)
            .Select(s => new { s.Date, CustomerType = s.Counterparty.Type })
            .ToListAsync(cancellationToken);

        var salesCategorized = data
            .GroupBy(i => new
            {
                i.CustomerType,
                TimeFrame = PanelMath.GetTimeFrameStart(i.Date, timeFrameUnit),
            })
            .Select(group => new CategorizedCustomer
            {
                Type = group.Key.CustomerType,
                Count = group.Count(),
                OnDate = group.Key.TimeFrame,
            })
            .OrderBy(x => x.Type)
            .ToList();

        var response = new CategorizedSales { Sales = salesCategorized };

        return ApiResponse<CategorizedSales>.Success(
            "Sales Fetched successfully", HttpStatusCode.OK, response);
    }
}
