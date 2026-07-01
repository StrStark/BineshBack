using Binesh.Application.Features.SalesReturns.Shared;
using MediatR;

namespace Binesh.Application.Features.SalesReturns.GetSalesReturnById;

public sealed record GetSalesReturnByIdQuery(Guid Id) : IRequest<SalesReturnDto>;
