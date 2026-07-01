using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;

namespace Binesh.Application.Features.SalesReturns.UpdateSalesReturn;

public sealed record UpdateSalesReturnCommand(
    Guid Id,
    DateTime? Date,
    long? Price,
    float? Quantity,
    float? DeliveredQuantity,
    int? DocNumber,
    Guid? ProductId,
    Guid? CounterpartyId)
    : IRequest<SalesReturnDto>;
