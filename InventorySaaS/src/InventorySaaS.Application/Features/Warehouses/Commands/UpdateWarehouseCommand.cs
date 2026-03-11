using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Warehouses.Commands;

public record UpdateWarehouseCommand(
    Guid WarehouseId,
    string? Name,
    string? Address,
    string? City,
    bool? IsDefault,
    bool? IsActive) : IRequest<Result<WarehouseDto>>;

public class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IApplicationDbContext _context;

    public UpdateWarehouseCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<WarehouseDto>> Handle(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.Locations)
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);

        if (warehouse is null)
            return Result<WarehouseDto>.Failure("Warehouse not found.");

        if (request.Name is not null) warehouse.Name = request.Name;
        if (request.Address is not null) warehouse.Address = request.Address;
        if (request.City is not null) warehouse.City = request.City;
        if (request.IsActive.HasValue) warehouse.IsActive = request.IsActive.Value;

        if (request.IsDefault == true)
        {
            var existingDefaults = await _context.Warehouses
                .Where(w => w.IsDefault && w.Id != warehouse.Id)
                .ToListAsync(cancellationToken);

            foreach (var w in existingDefaults)
                w.IsDefault = false;

            warehouse.IsDefault = true;
        }

        await _context.SaveChangesAsync(cancellationToken);

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
