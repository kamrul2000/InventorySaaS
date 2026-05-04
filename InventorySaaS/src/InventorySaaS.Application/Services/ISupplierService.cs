using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Suppliers.DTOs;

namespace InventorySaaS.Application.Services;

public interface ISupplierService
{
    Task<PaginatedList<SupplierDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken);

    Task<SupplierDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<SupplierDto> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken cancellationToken);

    Task<SupplierDto> UpdateAsync(
        Guid id,
        UpdateSupplierRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
