using Binesh.Application.Features.Customers.Shared;
using MediatR;

namespace Binesh.Application.Features.Customers.GetCustomerById;

public sealed record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerDto>;
