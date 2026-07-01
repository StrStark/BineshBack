using MediatR;

namespace Binesh.Application.Features.SalesReturns.DeleteSalesReturn;

public sealed record DeleteSalesReturnCommand(Guid Id) : IRequest;
