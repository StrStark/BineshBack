using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Customers.DeleteCustomer;

public sealed class DeleteCustomerHandler(IBineshDbContext db)
    : IRequestHandler<DeleteCustomerCommand, Unit>
{
    public async Task<Unit> Handle(DeleteCustomerCommand request, CancellationToken cancellationToken)
    {
        // EF's CASCADE goes principal -> dependent, and Person is the principal
        // here, so deleting Customer doesn't auto-delete Person. Person is owned
        // by exactly one Customer in our model, so we clean it up explicitly.
        // Region is intentionally NOT touched — it's shared across persons.
        var personId = await db.Customers
            .Where(c => c.Id == request.Id)
            .Select(c => (Guid?)c.PersonId)
            .SingleOrDefaultAsync(cancellationToken);

        if (personId is null)
        {
            throw new NotFoundException("Customer", request.Id);
        }

        await db.Customers.Where(c => c.Id == request.Id).ExecuteDeleteAsync(cancellationToken);
        await db.Persons.Where(p => p.Id == personId).ExecuteDeleteAsync(cancellationToken);

        return Unit.Value;
    }
}
