namespace InventorySaaS.Application.Features.Units.DTOs;

public record UnitOfMeasureDto(
    Guid Id,
    string Name,
    string Abbreviation,
    bool IsActive,
    int ProductCount);

public record CreateUnitOfMeasureRequest(
    string Name,
    string? Abbreviation);

public record UpdateUnitOfMeasureRequest(
    string? Name,
    string? Abbreviation,
    bool? IsActive);
