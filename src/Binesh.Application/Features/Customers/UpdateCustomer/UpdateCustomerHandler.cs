using Binesh.Application.Abstractions;
using Binesh.Application.Exceptions;
using Binesh.Application.Features.Customers.CreateCustomer;
using Binesh.Application.Features.Customers.Shared;
using Binesh.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Customers.UpdateCustomer;

public sealed class UpdateCustomerHandler(IBineshDbContext db)
    : IRequestHandler<UpdateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(UpdateCustomerCommand request, CancellationToken cancellationToken)
    {
        var customer = await db.Customers
            .Include(c => c.Person)
            .ThenInclude(p => p.Region)
            .SingleOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Customer", request.Id);

        customer.Update(request.Type, request.Active, request.PaymentReliability);

        if (request.Person is { } p)
        {
            var region = await ResolveRegionAsync(p.Region, cancellationToken);

            customer.Person.Update(
                p.Name,
                p.Family,
                p.Code,
                p.Phone,
                p.Mobile,
                p.Fax,
                p.Pelak,
                p.Address,
                p.BirthDate,
                region);
        }

        await db.SaveChangesAsync(cancellationToken);

        return CustomerProjection.ToDto(customer);
    }

    private async Task<Region?> ResolveRegionAsync(RegionInput? input, CancellationToken ct)
    {
        if (input is null
            || (string.IsNullOrWhiteSpace(input.Country)
                && string.IsNullOrWhiteSpace(input.Province)
                && string.IsNullOrWhiteSpace(input.City)))
        {
            return null;
        }

        var existing = await db.Regions.SingleOrDefaultAsync(r =>
            r.Country == input.Country
            && r.Province == input.Province
            && r.City == input.City,
            ct);

        return existing ?? Region.Create(input.Country, input.Province, input.City);
    }
}
