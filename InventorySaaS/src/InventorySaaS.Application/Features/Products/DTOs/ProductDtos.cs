namespace InventorySaaS.Application.Features.Products.DTOs;

public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    string? Barcode,
    Guid CategoryId,
    string CategoryName,
    Guid? BrandId,
    string? BrandName,
    Guid UnitOfMeasureId,
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
    bool? IsActive,
    /// <summary>
    /// A null <c>BrandId</c> means "leave the brand alone", so clearing one has to be explicit.
    /// </summary>
    bool ClearBrand = false);
