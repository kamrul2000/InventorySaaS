using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Warehouses.Queries;

public record GetWarehouseByIdQuery(Guid WarehouseId) : IRequest<Result<WarehouseDto>>;

public class GetWarehouseByIdQueryHandler : IRequestHandler<GetWarehouseByIdQuery, Result<WarehouseDto>>
{
    private readonly IApplicationDbContext _context;

    public GetWarehouseByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<WarehouseDto>> Handle(GetWarehouseByIdQuery request, CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => w.Id == request.WarehouseId && !w.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (warehouse is null)
            return Result<WarehouseDto>.Failure("Warehouse not found.");

        var dto = new WarehouseDto(
            warehouse.Id,
            warehouse.Name,
            warehouse.Code,
            warehouse.Address,
            warehouse.City,
            warehouse.IsDefault,
            warehouse.IsActive,
            warehouse.Locations.Count);

        return Result<WarehouseDto>.Success(dto);
    }
}
