using Binesh.Application.Features.Financial.CreateFinancialEntry;
using Binesh.Application.Features.Financial.DeleteFinancialEntry;
using Binesh.Application.Features.Financial.GetFinancialEntryById;
using Binesh.Application.Features.Financial.GetFinancialPanel;
using Binesh.Application.Features.Financial.GetMappingSettings;
using Binesh.Application.Features.Financial.ListFinancialEntries;
using Binesh.Application.Features.Financial.Shared;
using Binesh.Application.Features.Financial.UpdateFinancialEntry;
using Binesh.Application.Features.Financial.UpsertMappingSettings;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Financial;

public static class FinancialEndpoints
{
    public static IEndpointRouteBuilder MapFinancialEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/financial")
            .WithTags("Financial")
            .RequireAuthorization();

        group.MapGet("/entries", ListEntries)
            .WithName(nameof(ListEntries))
            .Produces<ListFinancialEntriesResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem();

        group.MapGet("/entries/{id:guid}", GetEntryById)
            .WithName(nameof(GetEntryById))
            .Produces<FinancialEntryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/entries", CreateEntry)
            .WithName(nameof(CreateEntry))
            .Produces<FinancialEntryDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/entries/{id:guid}", UpdateEntry)
            .WithName(nameof(UpdateEntry))
            .Produces<FinancialEntryDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        group.MapDelete("/entries/{id:guid}", DeleteEntry)
            .WithName(nameof(DeleteEntry))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/mapping-settings", GetSettings)
            .WithName(nameof(GetSettings))
            .Produces<MappingSettingsDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/mapping-settings", UpsertSettings)
            .WithName(nameof(UpsertSettings))
            .Produces<MappingSettingsDto>(StatusCodes.Status200OK);

        group.MapGet("/panel", GetPanel)
            .WithName(nameof(GetPanel))
            .Produces<GetFinancialPanelResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<IResult> ListEntries(
        string? search, string? type, int? page, int? pageSize,
        IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListFinancialEntriesQuery(search, type, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetEntryById(Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetFinancialEntryByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateEntry(
        [FromBody] CreateFinancialEntryCommand body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(body, ct);
        return Results.Created($"/api/financial/entries/{result.Id}", result);
    }

    private static async Task<IResult> UpdateEntry(
        Guid id, [FromBody] UpdateFinancialEntryBody body, IMediator mediator, CancellationToken ct)
    {
        var cmd = new UpdateFinancialEntryCommand(id, body.Code, body.Name, body.Type, body.Debit, body.Credit);
        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteEntry(Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteFinancialEntryCommand(id), ct);
        return Results.NoContent();
    }

    private static async Task<IResult> GetSettings(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetMappingSettingsQuery(), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> UpsertSettings(
        [FromBody] UpsertMappingSettingsCommand body, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(body, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetPanel(IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetFinancialPanelQuery(), ct);
        return Results.Ok(result);
    }

    public sealed record UpdateFinancialEntryBody(
        string? Code, string? Name, string? Type, long? Debit, long? Credit);
}
