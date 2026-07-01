using Binesh.Application.Features.Customers.CreateCustomer;
using Binesh.Application.Features.Customers.DeleteCustomer;
using Binesh.Application.Features.Customers.GetCustomerById;
using Binesh.Application.Features.Customers.ListCustomers;
using Binesh.Application.Features.Customers.Shared;
using Binesh.Application.Features.Customers.UpdateCustomer;
using Binesh.Domain.Customers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Binesh.Api.Endpoints.Customers;

public static class CustomersEndpoints
{
    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/customers")
            .WithTags("Customers")
            .RequireAuthorization();

        group.MapGet("/", ListCustomers)
            .WithName(nameof(ListCustomers))
            .Produces<ListCustomersResponse>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetCustomerById)
            .WithName(nameof(GetCustomerById))
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateCustomer)
            .WithName(nameof(CreateCustomer))
            .Produces<CustomerDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", UpdateCustomer)
            .WithName(nameof(UpdateCustomer))
            .Produces<CustomerDto>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteCustomer)
            .WithName(nameof(DeleteCustomer))
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        return routes;
    }

    private static async Task<IResult> ListCustomers(
        string? search,
        CustomerType? type,
        bool? active,
        int? page,
        int? pageSize,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(
            new ListCustomersQuery(search, type, active, page ?? 1, pageSize ?? 20), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetCustomerById(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCustomerByIdQuery(id), ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> CreateCustomer(
        [FromBody] CreateCustomerCommand body,
        IMediator mediator,
        CancellationToken ct)
    {
        var result = await mediator.Send(body, ct);
        return Results.Created($"/api/customers/{result.Id}", result);
    }

    private static async Task<IResult> UpdateCustomer(
        Guid id,
        [FromBody] UpdateUserBody body,
        IMediator mediator,
        CancellationToken ct)
    {
        // Bind the id from the route, the rest from the body.
        var cmd = new UpdateCustomerCommand(
            id, body.Type, body.Active, body.PaymentReliability, body.Person);
        var result = await mediator.Send(cmd, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> DeleteCustomer(
        Guid id, IMediator mediator, CancellationToken ct)
    {
        await mediator.Send(new DeleteCustomerCommand(id), ct);
        return Results.NoContent();
    }

    // Body shape for PUT — drops the Id so it can come from the route only.
    public sealed record UpdateUserBody(
        CustomerType? Type,
        bool? Active,
        float? PaymentReliability,
        PersonInput? Person);
}
