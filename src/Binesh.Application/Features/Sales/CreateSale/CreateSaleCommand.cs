using Binesh.Application.Features.Sales.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.CreateSale;

public sealed record CreateSaleCommand(
    DateTime Date,
    long Price,
    float Quantity,
    float DeliveredQuantity,
    int DocNumber,
    Guid ProductId,
    Guid CounterpartyId)
    : IRequest<SaleDto>;
