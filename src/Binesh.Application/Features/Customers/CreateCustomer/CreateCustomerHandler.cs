using Binesh.Application.Abstractions;
using Binesh.Application.Features.Customers.Shared;
using Binesh.Domain.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Binesh.Application.Features.Customers.CreateCustomer;

public sealed class CreateCustomerHandler(IBineshDbContext db, ITenantContext tenantContext)
    : IRequestHandler<CreateCustomerCommand, CustomerDto>
{
    public async Task<CustomerDto> Handle(CreateCustomerCommand request, CancellationToken cancellationToken)
    {
        var companyId = tenantContext.RequireCompanyId();
        var region = await ResolveRegionAsync(request.Person.Region, cancellationToken);

        var person = Person.Create(
            request.Person.Name,
            request.Person.Family,
            request.Person.Code,
            request.Person.Phone,
            request.Person.Mobile,
            request.Person.Fax,
            request.Person.Pelak,
            request.Person.Address,
            request.Person.BirthDate,
            region);

        var customer = Customer.Create(
            companyId,
            request.Type,
            request.Active,
            request.PaymentReliability,
            person);

        db.Customers.Add(customer);
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
