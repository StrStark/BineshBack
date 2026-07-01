using MediatR;

namespace Binesh.Application.Features.Customers.DeleteCustomer;

public sealed record DeleteCustomerCommand(Guid Id) : IRequest<Unit>;
