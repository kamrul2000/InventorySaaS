namespace InventorySaaS.Application.Features.Brands.DTOs;

public record BrandDto(
    Guid Id,
    string Name,
    string? Description,
    string? LogoUrl,
    bool IsActive,
    int ProductCount);

public record CreateBrandRequest(
    string Name,
    string? Description,
    string? LogoUrl);

public record UpdateBrandRequest(
    string? Name,
    string? Description,
    string? LogoUrl,
    bool? IsActive);
