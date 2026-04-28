namespace InventorySaaS.Application.Features.Products.DTOs;

/// <summary>
/// Draft product information extracted from an image. All fields are nullable
/// because the vision model is instructed to return <c>null</c> for anything
/// it cannot read confidently — the user fills the gaps in the UI before
/// submitting to <c>POST /api/v1/Products</c>.
/// </summary>
/// <param name="Name">Product name as visible on the packaging.</param>
/// <param name="Description">Concise inventory-catalog description (1–2 sentences).</param>
/// <param name="BrandName">Brand or manufacturer if clearly visible.</param>
/// <param name="UnitName">Unit of measure if visible (e.g. <c>kg</c>, <c>litre</c>, <c>piece</c>).</param>
/// <param name="Barcode">Barcode digits if a barcode is clearly readable.</param>
/// <param name="SuggestedCategory">A general category name to help the user pick from <c>GET /api/v1/Categories</c>.</param>
/// <param name="SuggestedSellingPrice">Retail price if printed on the package, as a plain number.</param>
/// <param name="SuggestedCostPrice">Cost price if visible (rare on consumer packaging).</param>
/// <param name="TrackExpiry"><c>true</c> if an expiry / best-before date is visible or the product type usually has one.</param>
/// <param name="Notes">Free-form hints for the user (size, low-confidence reason, etc.).</param>
public record ProductExtractionResult(
    string? Name,
    string? Description,
    string? BrandName,
    string? UnitName,
    string? Barcode,
    string? SuggestedCategory,
    decimal? SuggestedSellingPrice,
    decimal? SuggestedCostPrice,
    bool TrackExpiry,
    string? Notes);
