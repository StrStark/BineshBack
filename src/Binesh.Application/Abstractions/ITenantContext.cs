using Binesh.Application.Exceptions;

namespace Binesh.Application.Abstractions;

public interface ITenantContext
{
    Guid? CompanyId { get; }
}

public static class TenantContextExtensions
{
    public static Guid RequireCompanyId(this ITenantContext tenantContext)
    {
        return tenantContext.CompanyId
            ?? throw new ForbiddenException("The current user is not attached to a company.");
    }
}
