using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Categories.DTOs;

namespace InventorySaaS.Application.Services;

public interface ICategoryService
{
    Task<PaginatedList<CategoryDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken);

    Task<CategoryDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<CategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<CategoryDto> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
