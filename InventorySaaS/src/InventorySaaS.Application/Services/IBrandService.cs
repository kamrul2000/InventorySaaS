using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Brands.DTOs;

namespace InventorySaaS.Application.Services;

public interface IBrandService
{
    Task<PaginatedList<BrandDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken);

    Task<BrandDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<BrandDto> CreateAsync(
        CreateBrandRequest request,
        CancellationToken cancellationToken);

    Task<BrandDto> UpdateAsync(
        Guid id,
        UpdateBrandRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
