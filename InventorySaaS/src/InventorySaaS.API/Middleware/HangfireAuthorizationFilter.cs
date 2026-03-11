using Hangfire.Dashboard;

namespace InventorySaaS.API.Middleware;

public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In development, allow all access. In production, restrict to SuperAdmin.
        if (httpContext.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
            return true;

        return httpContext.User.IsInRole("SuperAdmin");
    }
}
