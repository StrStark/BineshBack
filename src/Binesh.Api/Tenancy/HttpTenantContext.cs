using System.Security.Claims;
using Binesh.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Binesh.Api.Tenancy;

internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public Guid? CompanyId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(TenantClaimTypes.CompanyId);
            return Guid.TryParse(value, out var companyId) ? companyId : null;
        }
    }
}
