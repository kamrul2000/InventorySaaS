using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;

namespace InventorySaaS.Application.Services;

public interface IWarehouseService
{
    Task<PaginatedList<WarehouseDto>> GetAllAsync(PaginationParams pagination, CancellationToken cancellationToken);
    Task<WarehouseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<WarehouseDto> CreateAsync(CreateWarehouseRequest request, CancellationToken cancellationToken);
    Task<WarehouseDto> UpdateAsync(Guid id, UpdateWarehouseRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
    Task<List<WarehouseLocationDto>> GetLocationsAsync(Guid warehouseId, CancellationToken cancellationToken);
    Task<WarehouseLocationDto> CreateLocationAsync(Guid warehouseId, CreateLocationRequest request, CancellationToken cancellationToken);
}
