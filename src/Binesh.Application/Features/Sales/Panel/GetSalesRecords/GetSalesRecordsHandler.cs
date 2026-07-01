using System.Net;
using Binesh.Application.Abstractions;
using Binesh.Application.Common;
using Binesh.Application.Features.Sales.Panel.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Sales.Panel.GetSalesRecords;

public sealed class GetSalesRecordsHandler(IBineshDbContext db)
    : IRequestHandler<GetSalesRecordsQuery, ApiResponse<PagedResult<SalesRecordsDto>>>
{
    public async Task<ApiResponse<PagedResult<SalesRecordsDto>>> Handle(
        GetSalesRecordsQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        var pageSize = Math.Min(req.Paggination.PageSize, 100); // max 100
        var pageNumber = req.Paggination.PageNumber < 1 ? 1 : req.Paggination.PageNumber;
        var category = req.CategoryDto.ProductCategory;
        var startTime = PanelMath.AsUtc(req.DateFilter.StartTime);
        var endTime = PanelMath.AsUtc(req.DateFilter.EndTime);

        var query = db.Sales
            .AsNoTracking()
            .Where(i =>
                i.Date >= startTime &&
                i.Date <= endTime &&
                (string.IsNullOrEmpty(category) || i.Product.DetailedType == category));

        if (!string.IsNullOrWhiteSpace(req.SearchTerm))
        {
            var search = req.SearchTerm.Trim();
            query = query.Where(c =>
                (c.Counterparty.Person.Family != null && c.Counterparty.Person.Family.Contains(search)) ||
                c.Counterparty.Person.Name.Contains(search) ||
                c.Product.ProductDescription.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Project the raw columns in SQL, then shape the DTO in memory so the
        // enum→string (ProductCategory) and name concatenation don't fight the
        // query translator. Ordering is newest-first for stable paging (the
        // legacy query had no ORDER BY, so page contents were nondeterministic).
        var rows = await query
            .OrderByDescending(s => s.Date)
            .ThenByDescending(s => s.DocNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new
            {
                s.DocNumber,
                s.Product.ProductDescription,
                ProductType = s.Product.Type,
                s.DeliveredQuantity,
                s.Counterparty.Person.Name,
                s.Counterparty.Person.Family,
                s.Price,
                s.Date,
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(s => new SalesRecordsDto
            {
                FactorNume = s.DocNumber,
                ProductDesc = s.ProductDescription,
                ProductCategory = s.ProductType.ToString(),
                DeliverdQuantity = s.DeliveredQuantity,
                CustomerName = s.Name + " " + s.Family,
                Price = s.Price,
                Date = s.Date,
            })
            .ToList();

        var paged = new PagedResult<SalesRecordsDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
        };

        return ApiResponse<PagedResult<SalesRecordsDto>>.Success(
            "Products fetched successfully", HttpStatusCode.OK, paged);
    }
}
