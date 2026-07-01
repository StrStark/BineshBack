using Binesh.Application.Abstractions;
using Binesh.Application.Features.Products.Shared;
using Binesh.Domain.Products;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Products.ListProducts;

public sealed record ListProductsQuery(
    string? Search,
    ProductType? Type,
    bool IncludeStats,
    int Page,
    int PageSize)
    : IRequest<ListProductsResponse>;

public sealed record ListProductsResponse(
    IReadOnlyList<ProductDto> Items,
    IReadOnlyList<ProductWithStatsDto>? ItemsWithStats,
    int TotalCount,
    int Page,
    int PageSize);

public sealed class ListProductsValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsValidator()
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, 200);
        RuleFor(q => q.Search).MaximumLength(200);
    }
}

public sealed class ListProductsHandler(IBineshDbContext db)
    : IRequestHandler<ListProductsQuery, ListProductsResponse>
{
    public async Task<ListProductsResponse> Handle(ListProductsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var s = request.Search.Trim();
            query = query.Where(p =>
                p.ProductCode.Contains(s)
                || p.ProductDescription.Contains(s)
                || p.DetailedType.Contains(s));
        }

        if (request.Type is { } t)
        {
            query = query.Where(p => p.Type == t);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        if (request.IncludeStats)
        {
            // Aggregates pushed to SQL — old code did this in C# after pulling rows.
            var rowsWithStats = await query
                .OrderBy(p => p.ProductDescription)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(p => new ProductWithStatsDto(
                    p.Id,
                    p.Type,
                    p.ProductCode,
                    p.ProductDescription,
                    p.DetailedType,
                    p.Events.Where(e => e.UnitPrice > 0).Sum(e => (long?)e.UnitPrice) ?? 0L,
                    p.Events.Where(e => e.TotalPrice > 0).Sum(e => (long?)e.TotalPrice) ?? 0L,
                    p.Events.Count))
                .ToListAsync(cancellationToken);

            return new ListProductsResponse(
                Items: [],
                ItemsWithStats: rowsWithStats,
                totalCount,
                request.Page,
                request.PageSize);
        }

        var rows = await query
            .OrderBy(p => p.ProductDescription)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new ListProductsResponse(
            rows.Select(ProductProjection.ToDto).ToList(),
            ItemsWithStats: null,
            totalCount,
            request.Page,
            request.PageSize);
    }
}
