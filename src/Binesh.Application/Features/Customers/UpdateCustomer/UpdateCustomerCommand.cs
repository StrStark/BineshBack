using Binesh.Application.Features.Customers.CreateCustomer;
using Binesh.Application.Features.Customers.Shared;
using Binesh.Domain.Customers;
using MediatR;

namespace Binesh.Application.Features.Customers.UpdateCustomer;

/// <summary>
/// Partial update. Any field set to null on the command means "leave unchanged";
/// to clear a field, pass an empty string (server-side trims/normalizes).
/// Person is updated in place; Region is re-resolved if provided.
/// </summary>
public sealed record UpdateCustomerCommand(
    Guid Id,
    CustomerType? Type,
    bool? Active,
    float? PaymentReliability,
    PersonInput? Person)
    : IRequest<CustomerDto>;
