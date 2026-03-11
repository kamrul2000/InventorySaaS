namespace InventorySaaS.Application.Features.Warehouses.DTOs;

public record WarehouseDto(
    Guid Id,
    string Name,
    string Code,
    string? Address,
    string? City,
    bool IsDefault,
    bool IsActive,
    int LocationCount);

public record WarehouseLocationDto(
    Guid Id,
    Guid WarehouseId,
    string Name,
    string? Aisle,
    string? Rack,
    string? Bin,
    bool IsActive);

public record CreateWarehouseRequest(
    string Name,
    string Code,
    string? Address,
    string? City,
    string? Country,
    string? ContactPerson,
    string? ContactPhone,
    bool IsDefault);

public record UpdateWarehouseRequest(
    string? Name,
    string? Address,
    string? City,
    bool? IsDefault,
    bool? IsActive);

public record CreateLocationRequest(
    string Name,
    string? Aisle,
    string? Rack,
    string? Bin,
    string? Description);
