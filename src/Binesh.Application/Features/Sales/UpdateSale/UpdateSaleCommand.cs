using Binesh.Application.Features.Sales.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.UpdateSale;

/// <summary>
/// Partial PATCH — null fields stay unchanged.
/// </summary>
public sealed record UpdateSaleCommand(
    Guid Id,
    DateTime? Date,
    long? Price,
    float? Quantity,
    float? DeliveredQuantity,
    int? DocNumber,
    Guid? ProductId,
    Guid? CounterpartyId)
    : IRequest<SaleDto>;
