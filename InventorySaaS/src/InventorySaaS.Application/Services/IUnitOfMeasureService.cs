using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Units.DTOs;

namespace InventorySaaS.Application.Services;

public interface IUnitOfMeasureService
{
    Task<PaginatedList<UnitOfMeasureDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureDto> CreateAsync(
        CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken);

    Task<UnitOfMeasureDto> UpdateAsync(
        Guid id,
        UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
