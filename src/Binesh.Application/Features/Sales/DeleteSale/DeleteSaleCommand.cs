using MediatR;

namespace Binesh.Application.Features.Sales.DeleteSale;

public sealed record DeleteSaleCommand(Guid Id) : IRequest;
