using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class WarehouseService : IWarehouseService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public WarehouseService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<WarehouseDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => !w.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(w =>
                w.Name.ToLower().Contains(searchTerm) ||
                w.Code.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => pagination.SortDescending ? query.OrderByDescending(w => w.Name) : query.OrderBy(w => w.Name),
            "code" => pagination.SortDescending ? query.OrderByDescending(w => w.Code) : query.OrderBy(w => w.Code),
            _ => query.OrderBy(w => w.Name)
        };

        var projected = query.Select(w => new WarehouseDto(
            w.Id, w.Name, w.Code, w.Address, w.City, w.IsDefault, w.IsActive, w.Locations.Count));

        return await PaginatedList<WarehouseDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<WarehouseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => w.Id == id && !w.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), id);

        return new WarehouseDto(
            warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.City,
            warehouse.IsDefault, warehouse.IsActive, warehouse.Locations.Count);
    }

    public async Task<WarehouseDto> CreateAsync(
        CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var codeExists = await _context.Warehouses
            .AnyAsync(w => w.Code == request.Code, cancellationToken);

        if (codeExists)
            throw new ConflictException("A warehouse with this code already exists.");

        if (request.IsDefault)
        {
            var existingDefault = await _context.Warehouses
                .Where(w => w.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var w in existingDefault)
                w.IsDefault = false;
        }

        var warehouse = new WarehouseInfo
        {
            TenantId = _currentUserService.TenantId!.Value,
            Name = request.Name,
            Code = request.Code,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            ContactPerson = request.ContactPerson,
            ContactPhone = request.ContactPhone,
            IsDefault = request.IsDefault,
            IsActive = true
        };

        _context.Warehouses.Add(warehouse);
        await _context.SaveChangesAsync(cancellationToken);

        return new WarehouseDto(
            warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.City,
            warehouse.IsDefault, warehouse.IsActive, 0);
    }

    public async Task<WarehouseDto> UpdateAsync(
        Guid id,
        UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .Include(w => w.Locations)
            .FirstOrDefaultAsync(w => w.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), id);

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

        return new WarehouseDto(
            warehouse.Id, warehouse.Name, warehouse.Code, warehouse.Address, warehouse.City,
            warehouse.IsDefault, warehouse.IsActive, warehouse.Locations.Count);
    }

    public async Task<WarehouseLocationDto> CreateLocationAsync(
        Guid warehouseId,
        CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var warehouseExists = await _context.Warehouses
            .AnyAsync(w => w.Id == warehouseId, cancellationToken);

        if (!warehouseExists)
            throw new NotFoundException(nameof(WarehouseInfo), warehouseId);

        var location = new WarehouseLocation
        {
            TenantId = _currentUserService.TenantId!.Value,
            WarehouseId = warehouseId,
            Name = request.Name,
            Aisle = request.Aisle,
            Rack = request.Rack,
            Bin = request.Bin,
            Description = request.Description,
            IsActive = true
        };

        _context.WarehouseLocations.Add(location);
        await _context.SaveChangesAsync(cancellationToken);

        return new WarehouseLocationDto(
            location.Id, location.WarehouseId, location.Name,
            location.Aisle, location.Rack, location.Bin, location.IsActive);
    }
}
