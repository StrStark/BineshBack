using Binesh.Application.Features.SalesReturns.CreateSalesReturn;
using Binesh.Application.Features.SalesReturns.DeleteSalesReturn;
using Binesh.Application.Features.SalesReturns.GetReturnsSummary;
using Binesh.Application.Features.SalesReturns.GetSalesReturnById;
using Binesh.Application.Features.SalesReturns.ListSalesReturns;
using Binesh.Application.Features.SalesReturns.Shared;
using Binesh.Application.Features.SalesReturns.UpdateSalesReturn;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.SalesReturns;

/// <summary>
/// Round 10 — Sales Returns. Mirrors the Sales feature: CRUD + summary.
/// Categorized / regional / top-selling panels are deferred; in the legacy
/// UI returns only showed up as a <c>ReturnTotal</c> card in the unified
/// sales summary, which the panel layer can compose from <c>/api/sales/summary</c>
/// + <c>/api/sales-returns/summary</c> on its own.
/// </summary>
public static class SalesReturnsEndpoints
{
    public static IEndpointRouteBuilder MapSalesReturnsEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/sales-returns")
            .WithTags("SalesReturns")
            .RequireAuthorization();

        group.MapGet("/summary", GetReturnsSummary)
            .WithName(nameof(GetReturnsSummary))
            .WithSummary("Per-day returns aggregation for a date range.")
            .Produces<GetReturnsSummaryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/", ListSalesReturns)
            .WithName(nameof(ListSalesReturns))
            .Produces<ListSalesReturnsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetSalesReturnById)
            .WithName(nameof(GetSalesReturnById))
            .Produces<SalesReturnDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateSalesReturn)
            .WithName(nameof(CreateSalesReturn))
            .Produces<SalesReturnDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateSalesReturn)
            .WithName(nameof(UpdateSalesReturn))
            .Produces<SalesReturnDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", DeleteSalesReturn)
            .WithName(nameof(DeleteSalesReturn))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    private static async Task<IResult> GetReturnsSummary(
        DateOnly from, DateOnly to, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetReturnsSummaryQuery(from, to), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ListSalesReturns(
        DateOnly? from, DateOnly? to, Guid? customerId, Guid? productId, string? search,
        int? page, int? pageSize, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListSalesReturnsQuery(from, to, customerId, productId, search, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSalesReturnById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetSalesReturnByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateSalesReturn(
        [FromBody] CreateSalesReturnCommand body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(body, ct);
        return Results.Created($"/api/sales-returns/{result.Id}", result);
    }

    private static async Task<IResult> UpdateSalesReturn(
        Guid id, [FromBody] UpdateSalesReturnBody body, IMediator mediator, CancellationToken ct)
    {
        var cmd = new UpdateSalesReturnCommand(
            id, body.Date, body.Price, body.Quantity, body.DeliveredQuantity,
            body.DocNumber, body.ProductId, body.CounterpartyId);
        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteSalesReturn(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteSalesReturnCommand(id), ct);
        return Results.NoContent();
    }

    public sealed record UpdateSalesReturnBody(
        DateTime? Date,
        long? Price,
        float? Quantity,
        float? DeliveredQuantity,
        int? DocNumber,
        Guid? ProductId,
        Guid? CounterpartyId);
}
