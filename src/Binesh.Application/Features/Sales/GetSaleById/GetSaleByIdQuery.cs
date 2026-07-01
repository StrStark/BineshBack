using Binesh.Application.Features.Sales.Shared;
using MediatR;

namespace Binesh.Application.Features.Sales.GetSaleById;

public sealed record GetSaleByIdQuery(Guid Id) : IRequest<SaleDto>;
