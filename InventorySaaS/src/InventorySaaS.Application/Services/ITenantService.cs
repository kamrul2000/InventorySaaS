using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Tenants.DTOs;

namespace InventorySaaS.Application.Services;

public interface ITenantService
{
    Task<PaginatedList<TenantDto>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken);
    Task<TenantDto> GetCurrentAsync(CancellationToken cancellationToken);
    Task<TenantDto> UpdateCurrentAsync(UpdateTenantRequest request, CancellationToken cancellationToken);
}
