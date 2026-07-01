using Binesh.Application.Features.Products.AddInventoryEvent;
using Binesh.Application.Features.Products.ClearInventoryEvents;
using Binesh.Application.Features.Products.CreateProduct;
using Binesh.Application.Features.Products.DeleteProduct;
using Binesh.Application.Features.Products.GetProductByCode;
using Binesh.Application.Features.Products.GetProductById;
using Binesh.Application.Features.Products.GetStagnationReport;
using Binesh.Application.Features.Products.ListInventoryEvents;
using Binesh.Application.Features.Products.ListProducts;
using Binesh.Application.Features.Products.Shared;
using Binesh.Application.Features.Products.UpdateProduct;
using Binesh.Domain.Products;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Products;

public static class ProductsEndpoints
{
    public static IEndpointRouteBuilder MapProductsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization();

        // ── CRUD ────────────────────────────────────────────────────────────
        group.MapGet("/", ListProducts)
            .WithName(nameof(ListProducts))
            .Produces<ListProductsResponse>(StatusCodes.Status200OK);

        group.MapGet("/by-code/{code}", GetByCode)
            .WithName(nameof(GetByCode))
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}", GetById)
            .WithName(nameof(GetById))
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateProduct)
            .WithName(nameof(CreateProduct))
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateProduct)
            .WithName(nameof(UpdateProduct))
            .Produces<ProductDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", DeleteProduct)
            .WithName(nameof(DeleteProduct))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // ── Inventory events ────────────────────────────────────────────────
        group.MapGet("/{id:guid}/events", ListEvents)
            .WithName(nameof(ListEvents))
            .Produces<ListInventoryEventsResponse>(StatusCodes.Status200OK);

        group.MapPost("/{id:guid}/events", AddEvent)
            .WithName(nameof(AddEvent))
            .Produces<InventoryEventDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}/events", ClearEvents)
            .WithName(nameof(ClearEvents))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        // ── Analytics ───────────────────────────────────────────────────────
        group.MapGet("/stagnation", Stagnation)
            .WithName(nameof(Stagnation))
            .Produces<StagnationReportResponse>(StatusCodes.Status200OK);

        return routes;
    }

    private static async Task<IResult> ListProducts(
        string? search,
        ProductType? type,
        bool? includeStats,
        int? page,
        int? pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListProductsQuery(search, type, includeStats ?? false, page ?? 1, pageSize ?? 20),
            ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetByCode(string code, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new GetProductByCodeQuery(code), ct));

    private static async Task<IResult> GetById(Guid id, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new GetProductByIdQuery(id), ct));

    private static async Task<IResult> CreateProduct(
        [FromBody] CreateProductCommand body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(body, ct);
        return Results.Created($"/api/products/{result.Id}", result);
    }

    private static async Task<IResult> UpdateProduct(
        Guid id, [FromBody] UpdateProductBody body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateProductCommand(id, body.Type, body.ProductCode, body.ProductDescription, body.DetailedType),
            ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteProduct(Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteProductCommand(id), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ListEvents(
        Guid id, int? page, int? pageSize, IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(
            new ListInventoryEventsQuery(id, page ?? 1, pageSize ?? 50), ct));

    private static async Task<IResult> AddEvent(
        Guid id, [FromBody] AddEventBody body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new AddInventoryEventCommand(
            id, body.Type, body.Date, body.Quantity, body.UnitPrice, body.TotalPrice,
            body.FactorNumber, body.Value1, body.Value2, body.Value3, body.Description), ct);
        return Results.Created($"/api/products/{id}/events/{result.Id}", result);
    }

    private static async Task<IResult> ClearEvents(Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new ClearInventoryEventsCommand(id), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Stagnation(IMediator mediator, CancellationToken ct) =>
        Results.Ok(await mediator.Send(new GetStagnationReportQuery(), ct));

    // ── Body DTOs (id comes from the route) ─────────────────────────────────

    public sealed record UpdateProductBody(
        ProductType? Type,
        string? ProductCode,
        string? ProductDescription,
        string? DetailedType);

    public sealed record AddEventBody(
        InventoryEventType Type,
        DateTime Date,
        float Quantity,
        long UnitPrice,
        long TotalPrice,
        int FactorNumber,
        string? Value1,
        string? Value2,
        string? Value3,
        string? Description);
}
