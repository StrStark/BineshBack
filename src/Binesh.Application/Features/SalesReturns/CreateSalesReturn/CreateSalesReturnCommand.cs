using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;

namespace Binesh.Application.Features.SalesReturns.CreateSalesReturn;

public sealed record CreateSalesReturnCommand(
    DateTime Date,
    long Price,
    float Quantity,
    float DeliveredQuantity,
    int DocNumber,
    Guid ProductId,
    Guid CounterpartyId)
    : IRequest<SalesReturnDto>;
