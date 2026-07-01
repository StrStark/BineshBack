using System.Net;
using Binesh.Application.Abstractions;
using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.Panel.GetPanelSummary;

public sealed class GetPanelSummaryHandler(IBineshDbContext db)
    : IRequestHandler<GetPanelSummaryQuery, ApiResponse<SalesSummaryDto>>
{
    public async Task<ApiResponse<SalesSummaryDto>> Handle(
        GetPanelSummaryQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var start = PanelMath.AsUtc(req.DateFilter.StartTime);
        var end = PanelMath.AsUtc(req.DateFilter.EndTime);
        var duration = end - start;
        var startBefore = start - duration;
        var category = req.CategoryDto.ProductCategory;

        // One query per table spanning [previous window .. current window]; the
        // current/previous split happens in memory (small dataset).
        var salesData = await db.Sales
            .AsNoTracking()
            .Where(s =>
                s.Date >= startBefore &&
                s.Date <= end &&
                (string.IsNullOrEmpty(category) || s.Product.DetailedType == category))
            .Select(s => new { s.Date, s.Price, Type = s.Product.DetailedType })
            .ToListAsync(cancellationToken);

        var returnData = await db.SalesReturns
            .AsNoTracking()
            .Where(r =>
                r.Date >= startBefore &&
                r.Date <= end &&
                (string.IsNullOrEmpty(category) || r.Product.DetailedType == category))
            .Select(r => new { r.Date, r.Price, Type = r.Product.DetailedType })
            .ToListAsync(cancellationToken);

        var salesCurrent = salesData.Where(x => x.Date >= start).ToList();
        var salesBefore = salesData.Where(x => x.Date < start);
        var returnsCurrent = returnData.Where(x => x.Date >= start);
        var returnsBefore = returnData.Where(x => x.Date < start);

        var soldItems = salesCurrent
            .GroupBy(x => x.Type)
            .Select(g => new SoldItem { Type = g.Key, Value = g.Sum(x => x.Price) })
            .ToList();

        long soldItemsBeforeTotal = salesBefore.Sum(x => x.Price);
        long soldItemsTotal = soldItems.Sum(x => x.Value);

        var returnedItems = returnsCurrent
            .GroupBy(x => x.Type)
            .Select(g => new { Type = g.Key, Value = g.Sum(x => x.Price) })
            .ToList();

        long returnedItemsBeforeTotal = returnsBefore.Sum(x => x.Price);
        long returnedItemsTotal = returnedItems.Sum(x => x.Value);

        var returnDict = returnedItems.ToDictionary(r => r.Type!, r => r.Value);

        var soldItemsWithReturned = soldItems
            .Select(s => new SoldItem
            {
                Type = s.Type,
                Value = s.Value,
                Returned = s.Value != 0 && returnDict.TryGetValue(s.Type!, out var rv)
                    ? (float?)Math.Round((float)rv / s.Value * 100, 1)
                    : 0,
            })
            .ToList();

        var response = new SalesSummaryDto
        {
            SoldItems = soldItemsWithReturned,
            Count = salesCurrent.Count,
            Sum = soldItemsTotal,
            SalesCards = new SalesCardsDto
            {
                TotalSales = new Card<float>
                {
                    Growth = PanelMath.CalculateGrowth(soldItemsTotal, soldItemsBeforeTotal),
                    Value = soldItemsTotal,
                },
                ReturnTotal = new Card<float>
                {
                    Growth = PanelMath.CalculateGrowth(returnedItemsTotal, returnedItemsBeforeTotal),
                    Value = returnedItemsTotal,
                },
                // OffSales / NewModelsSales were never computed in the legacy code — left null.
            },
        };

        return ApiResponse<SalesSummaryDto>.Success(
            "Sales Fetched successfully", HttpStatusCode.OK, response);
    }
}
