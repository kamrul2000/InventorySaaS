namespace InventorySaaS.Application.Features.Products.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    string CategoryName,
    string? BrandName,
    string UnitName,
    decimal CostPrice,
    decimal SellingPrice,
    int ReorderLevel,
    bool TrackExpiry,
    bool IsActive,
    DateTime CreatedAt);

public record CreateProductRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    Guid? BrandId,
    Guid? UnitOfMeasureId,
    decimal CostPrice,
    decimal SellingPrice,
    int? ReorderLevel,
    string? Barcode,
    bool TrackExpiry,
    int? MinimumOrderQuantity,
    string? BrandName,
    string? UnitName);

public record UpdateProductRequest(
    string? Name,
    string? Description,
    Guid? CategoryId,
    Guid? BrandId,
    Guid? UnitOfMeasureId,
    decimal? CostPrice,
    decimal? SellingPrice,
    int? ReorderLevel,
    string? Barcode,
    bool? TrackExpiry,
    bool? IsActive);

public record BrandDto(Guid Id, string Name, string? Description, bool IsActive);

public record UnitOfMeasureDto(Guid Id, string Name, string Abbreviation, bool IsActive);
