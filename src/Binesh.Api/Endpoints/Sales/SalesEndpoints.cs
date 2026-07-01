using Binesh.Application.Common;
using Binesh.Application.Features.Sales.CreateSale;
using Binesh.Application.Features.Sales.DeleteSale;
using Binesh.Application.Features.Sales.GetSaleById;
using Binesh.Application.Features.Sales.GetSummary;
using Binesh.Application.Features.Sales.ListSales;
using Binesh.Application.Features.Sales.Panel.GetCategorizedCustomerSales;
using Binesh.Application.Features.Sales.Panel.GetPanelSummary;
using Binesh.Application.Features.Sales.Panel.GetRegionalSales;
using Binesh.Application.Features.Sales.Panel.GetSalesRecords;
using Binesh.Application.Features.Sales.Panel.GetTopSellingPanelProducts;
using Binesh.Application.Features.Sales.Panel.Shared;
using Binesh.Application.Features.Sales.Shared;
using Binesh.Application.Features.Sales.UpdateSale;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Sales;

/// <summary>
/// All Sales HTTP endpoints.
///
/// Round 9 added full CRUD (List, GetById, Create, Update, Delete) on top of
/// the Round 4 reference <c>/summary</c> endpoint. Panel summary endpoints
/// (Categorized, Regional, TopSelling) land in Round 9b.
/// </summary>
public static class SalesEndpoints
{
    public static IEndpointRouteBuilder MapSalesEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/sales")
            .WithTags("Sales")
            .RequireAuthorization();

        group.MapGet("/summary", GetSummary)
            .WithName(nameof(GetSummary))
            .WithSummary("Per-day revenue + order-count summary for a date range.")
            .Produces<GetSummaryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/", ListSales)
            .WithName(nameof(ListSales))
            .Produces<ListSalesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetSaleById)
            .WithName(nameof(GetSaleById))
            .Produces<SaleDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateSale)
            .WithName(nameof(CreateSale))
            .Produces<SaleDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateSale)
            .WithName(nameof(UpdateSale))
            .Produces<SaleDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", DeleteSale)
            .WithName(nameof(DeleteSale))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // ── Panel endpoints (legacy SalesApiController parity) ──────────────
        // POST + body, ApiResponse<T> envelope, identical business logic to the
        // old panel. Routes are the new REST-style /api/sales/panel/*.
        var panel = group.MapGroup("/panel").WithTags("Sales Panel");

        panel.MapPost("/summary", GetPanelSummary)
            .WithName("GetSalesPanelSummary")
            .WithSummary("Sold items by category + total/return cards with growth. (legacy GetSalesSummaryAsync)")
            .Produces<ApiResponse<SalesSummaryDto>>(StatusCodes.Status200OK);

        panel.MapPost("/categorized-customers", GetCategorizedCustomers)
            .WithName("GetCategorizedCustomerSales")
            .WithSummary("Sales counts by customer type + time bucket. (legacy GetCustomercategorizedSalesAsync)")
            .Produces<ApiResponse<CategorizedSales>>(StatusCodes.Status200OK);

        panel.MapPost("/regional", GetRegionalSales)
            .WithName("GetRegionalSales")
            .WithSummary("Regional sales. (legacy GetProvinceategorizdeSalesAsync — returns empty, parity)")
            .Produces<ApiResponse<RegionalSalesDto>>(StatusCodes.Status200OK);

        panel.MapPost("/records", GetSalesRecords)
            .WithName("GetSalesRecords")
            .WithSummary("Paginated, searchable sales rows for the panel table. (legacy GetSalesRecords)")
            .Produces<ApiResponse<PagedResult<SalesRecordsDto>>>(StatusCodes.Status200OK);

        panel.MapPost("/top-selling", GetTopSellingPanelProducts)
            .WithName("GetTopSellingPanelProducts")
            .WithSummary("Top 5 products by delivered quantity with growth. (legacy GetTopSellingProductsAsync)")
            .Produces<ApiResponse<TopSellingProductsDto>>(StatusCodes.Status200OK);

        return routes;
    }

    // ── Panel handlers ──────────────────────────────────────────────────────

    private static async Task<IResult> GetPanelSummary(
        [FromBody] SalesPageRequestDto body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPanelSummaryQuery(body), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCategorizedCustomers(
        [FromBody] SalesPageRequestDto body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCategorizedCustomerSalesQuery(body), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetRegionalSales(
        [FromBody] SalesPageRequestDto body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRegionalSalesQuery(body), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSalesRecords(
        [FromBody] SalesPageRequestPaginatedDto body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSalesRecordsQuery(body), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTopSellingPanelProducts(
        [FromBody] SalesPageRequestDto body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetTopSellingPanelProductsQuery(body), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSummary(
        DateOnly from,
        DateOnly to,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(new GetSummaryQuery(from, to), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListSales(
        DateOnly? from,
        DateOnly? to,
        Guid? customerId,
        Guid? productId,
        string? search,
        int? page,
        int? pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListSalesQuery(from, to, customerId, productId, search, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSaleById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSaleByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateSale(
        [FromBody] CreateSaleCommand body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(body, ct);
        return Results.Created($"/api/sales/{result.Id}", result);
    }

    private static async Task<IResult> UpdateSale(
        Guid id,
        [FromBody] UpdateSaleBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        var cmd = new UpdateSaleCommand(
            id,
            body.Date,
            body.Price,
            body.Quantity,
            body.DeliveredQuantity,
            body.DocNumber,
            body.ProductId,
            body.CounterpartyId);
        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteSale(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteSaleCommand(id), ct);
        return Results.NoContent();
    }

    // Body shape for PUT — drops the route Id from the JSON body.
    public sealed record UpdateSaleBody(
        DateTime? Date,
        long? Price,
        float? Quantity,
        float? DeliveredQuantity,
        int? DocNumber,
        Guid? ProductId,
        Guid? CounterpartyId);
}
