using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Warehouse;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Warehouses.Commands;

public record CreateLocationCommand(
    Guid WarehouseId,
    string Name,
    string? Aisle,
    string? Rack,
    string? Bin,
    string? Description) : IRequest<Result<WarehouseLocationDto>>;

public class CreateLocationCommandHandler : IRequestHandler<CreateLocationCommand, Result<WarehouseLocationDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateLocationCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<WarehouseLocationDto>> Handle(CreateLocationCommand request, CancellationToken cancellationToken)
    {
        var warehouseExists = await _context.Warehouses
            .AnyAsync(w => w.Id == request.WarehouseId, cancellationToken);

        if (!warehouseExists)
            return Result<WarehouseLocationDto>.Failure("Warehouse not found.");

        var location = new WarehouseLocation
        {
            TenantId = _currentUserService.TenantId!.Value,
            WarehouseId = request.WarehouseId,
            Name = request.Name,
            Aisle = request.Aisle,
            Rack = request.Rack,
            Bin = request.Bin,
            Description = request.Description,
            IsActive = true
        };

        _context.WarehouseLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new WarehouseLocationDto(
            location.Id,
            location.WarehouseId,
            location.Name,
            location.Aisle,
            location.Rack,
            location.Bin,
            location.IsActive);

        return Result<WarehouseLocationDto>.Success(dto);
    }
}
