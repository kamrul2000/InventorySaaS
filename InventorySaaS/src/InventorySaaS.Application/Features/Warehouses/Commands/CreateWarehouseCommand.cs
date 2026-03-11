using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Warehouse;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Warehouses.Commands;

public record CreateWarehouseCommand(
    string Name,
    string Code,
    string? Address,
    string? City,
    string? Country,
    string? ContactPerson,
    string? ContactPhone,
    bool IsDefault) : IRequest<Result<WarehouseDto>>;

public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Result<WarehouseDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateWarehouseCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<WarehouseDto>> Handle(CreateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var codeExists = await _context.Warehouses
            .AnyAsync(w => w.Code == request.Code, cancellationToken);

        if (codeExists)
            return Result<WarehouseDto>.Failure("A warehouse with this code already exists.");

        // If this is the default warehouse, unset previous default
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

        var dto = new WarehouseDto(
            warehouse.Id,
            warehouse.Name,
            warehouse.Code,
            warehouse.Address,
            warehouse.City,
            warehouse.IsDefault,
            warehouse.IsActive,
            0);

        return Result<WarehouseDto>.Success(dto);
    }
}
