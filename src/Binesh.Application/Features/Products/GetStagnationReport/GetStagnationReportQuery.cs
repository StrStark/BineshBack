using Binesh.Application.Abstractions;
using Binesh.Domain.Products;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.GetStagnationReport;

/// <summary>
/// FIFO inventory stagnation: for each product, what stock is still on hand
/// after consuming all Issue/SalesInvoice events against the Receipt events,
/// and how long has that remaining stock been sitting?
///
/// Algorithm:
///   - Walk events oldest-first.
///   - Receipt → enqueue (date, qty, unitPrice).
///   - Issue / SalesInvoice → dequeue oldest stock first (FIFO).
///   - Remaining queue items = stagnant stock; weighted-average their age.
/// </summary>
public sealed record GetStagnationReportQuery : IRequest<StagnationReportResponse>;

public sealed record StagnationReportResponse(IReadOnlyList<StagnationPointDto> Points);

public sealed record StagnationPointDto(
    Guid ProductId,
    string ProductCode,
    ProductType Category,
    double WeightedAverageAgeDays,
    long LatestUnitPrice,
    float CurrentStock,
    long TotalStagnationValue);

public sealed class GetStagnationReportHandler(IBineshDbContext db)
    : IRequestHandler<GetStagnationReportQuery, StagnationReportResponse>
{
    public async Task<StagnationReportResponse> Handle(GetStagnationReportQuery request, CancellationToken cancellationToken)
    {
        // Single round-trip: product header + ordered events for everything.
        var products = await db.Products
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.ProductCode,
                p.Type,
                Events = p.Events
                    .OrderBy(e => e.Date)
                    .Select(e => new { e.Type, e.Date, e.Quantity, e.UnitPrice })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var today = DateTime.UtcNow;

        var points = products
            .Select(p => Compute(p.Id, p.ProductCode, p.Type, p.Events, today))
            .Where(point => point is not null)
            .ToList();

        return new StagnationReportResponse(points!);

        static StagnationPointDto? Compute(
            Guid productId,
            string productCode,
            ProductType type,
            IReadOnlyList<dynamic> events,
            DateTime today)
        {
            // FIFO queue: each entry is (entryDate, remainingQty, unitPrice).
            var stockQueue = new Queue<(DateTime EntryDate, float Quantity, long UnitPrice)>();
            long latestPrice = 0L;

            foreach (var ev in events)
            {
                if (ev.Type == InventoryEventType.Receipt)
                {
                    stockQueue.Enqueue((ev.Date, ev.Quantity, ev.UnitPrice));
                    latestPrice = ev.UnitPrice;
                }
                else if (ev.Type == InventoryEventType.Issue
                      || ev.Type == InventoryEventType.SalesInvoice)
                {
                    float toDeduct = ev.Quantity;
                    while (toDeduct > 0f && stockQueue.Count > 0)
                    {
                        var batch = stockQueue.Dequeue();
                        if (batch.Quantity > toDeduct)
                        {
                            stockQueue.Enqueue((batch.EntryDate, batch.Quantity - toDeduct, batch.UnitPrice));
                            toDeduct = 0f;
                        }
                        else
                        {
                            toDeduct -= batch.Quantity;
                        }
                    }
                }
            }

            if (stockQueue.Count == 0)
            {
                return null;
            }

            var totalQty = stockQueue.Sum(x => x.Quantity);
            var avgAgeDays = totalQty == 0f
                ? 0d
                : stockQueue.Sum(x => x.Quantity * (today - x.EntryDate).TotalDays) / totalQty;

            return new StagnationPointDto(
                productId,
                productCode,
                type,
                avgAgeDays,
                latestPrice,
                totalQty,
                (long)(totalQty * latestPrice));
        }
    }
}
