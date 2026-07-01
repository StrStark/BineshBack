using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Customers.Shared;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Customers.GetCustomerById;

public sealed class GetCustomerByIdHandler(IBineshDbContext db)
    : IRequestHandler<GetCustomerByIdQuery, CustomerDto>
{
    public async Task<CustomerDto> Handle(GetCustomerByIdQuery request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(c => c.Person)
            .ThenInclude(p => p.Region)
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer", request.Id);

        return CustomerProjection.ToDto(customer);
    }
}
