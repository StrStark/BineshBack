using System.Net;
using Binesh.Application.Abstractions;
using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.Panel.GetTopSellingPanelProducts;

public sealed class GetTopSellingPanelProductsHandler(IBineshDbContext db)
    : IRequestHandler<GetTopSellingPanelProductsQuery, ApiResponse<TopSellingProductsDto>>
{
    public async Task<ApiResponse<TopSellingProductsDto>> Handle(
        GetTopSellingPanelProductsQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var start = PanelMath.AsUtc(req.DateFilter.StartTime);
        var end = PanelMath.AsUtc(req.DateFilter.EndTime);
        var duration = end - start;
        var startBefore = start - duration;

        var salesData = await db.Sales
            .AsNoTracking()
            .Where(s => s.Date >= startBefore && s.Date <= end)
            .Select(s => new
            {
                s.Date,
                s.DeliveredQuantity,
                s.Price,
                ProductName = s.Product.ProductDescription,
            })
            .ToListAsync(cancellationToken);

        var current = salesData.Where(x => x.Date >= start).ToList();
        var previous = salesData.Where(x => x.Date < start);

        var previousDict = previous
            .GroupBy(x => x.ProductName)
            .ToDictionary(g => g.Key!, g => g.Sum(x => x.DeliveredQuantity));

        var topProducts = current
            .GroupBy(x => x.ProductName)
            .Select(g =>
            {
                var currentCount = g.Sum(x => x.DeliveredQuantity);
                previousDict.TryGetValue(g.Key!, out var prevCount);
                return new
                {
                    ProductName = g.Key,
                    Count = currentCount,
                    TotalAmount = (float)g.Sum(x => x.Price),
                    Growth = PanelMath.CalculateGrowth(currentCount, prevCount),
                };
            })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToList();

        var result = topProducts
            .Select((x, index) => new TopProductItemDto
            {
                Rank = index + 1,
                ProductName = x.ProductName!,
                Count = (int)x.Count,
                TotalAmount = x.TotalAmount,
                Growth = x.Growth,
            })
            .ToList();

        var response = new TopSellingProductsDto { Items = result };

        return ApiResponse<TopSellingProductsDto>.Success(
            "Top products fetched successfully", HttpStatusCode.OK, response);
    }
}
