using InventorySaaS.Application.Features.Dashboard.DTOs;

namespace InventorySaaS.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken);
}
