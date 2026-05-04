using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;

namespace InventorySaaS.Application.Services;

public interface IProductService
{
    Task<PaginatedList<ProductDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? categoryId,
        Guid? brandId,
        bool? isActive,
        CancellationToken cancellationToken);

    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
    Task<ProductDto> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
