namespace InventorySaaS.API.Middleware;

public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip tenant resolution for auth endpoints and health checks
        var path = context.Request.Path.Value?.ToLower() ?? "";
        if (path.StartsWith("/api/v1/auth") || path.StartsWith("/health") || path.StartsWith("/hangfire") || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        // Tenant is resolved from JWT claim by TenantAccessor
        // This middleware just logs tenant context
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrEmpty(tenantClaim))
        {
            _logger.LogDebug("Request for tenant: {TenantId}", tenantClaim);
        }

        await _next(context);
    }
}
