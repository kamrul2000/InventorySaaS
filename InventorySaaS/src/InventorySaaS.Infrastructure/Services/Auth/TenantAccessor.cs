using InventorySaaS.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace InventorySaaS.Infrastructure.Services.Auth;

public class TenantAccessor : ITenantAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? TenantId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context == null) return null;

            // 1. Try from JWT claim
            var claimTenantId = context.User?.FindFirst("tenant_id")?.Value;
            if (!string.IsNullOrEmpty(claimTenantId) && Guid.TryParse(claimTenantId, out var fromClaim))
                return fromClaim;

            // 2. Try from header (dev/local)
            if (context.Request.Headers.TryGetValue("X-TenantId", out var headerTenantId)
                && Guid.TryParse(headerTenantId, out var fromHeader))
                return fromHeader;

            // 3. Try from subdomain (production)
            var host = context.Request.Host.Host;
            if (host.Contains('.'))
            {
                // subdomain resolution would be done via database lookup
                // For now, return null and let middleware handle it
            }

            return null;
        }
    }
}
