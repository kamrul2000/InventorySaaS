using InventorySaaS.Application.Common.Barcodes;

namespace InventorySaaS.Application.Features.Scan.DTOs;

public record ResolveScanRequest(
    string RawValue,
    /// <summary>
    /// Optional filter naming the kinds the caller is willing to accept — a picking screen
    /// scanning for a bin does not want a product match. Names match <c>ScanKind</c>.
    /// </summary>
    IReadOnlyList<string>? ExpectedKinds = null);

/// <summary>
/// What a scanned string turned out to be. Exactly one of the payload properties is populated,
/// selected by <paramref name="Kind"/>; an unrecognised scan returns <c>Unknown</c> with a
/// <paramref name="Message"/> rather than an error status.
/// </summary>
public record ScanResultDto(
    string Kind,
    bool Matched,
    string RawValue,
    ScanPayloadDto Payload,
    string? Message = null,
    ScannedProductDto? Product = null,
    ScannedLocationDto? Location = null,
    ScannedWarehouseDto? Warehouse = null,
    ScannedSerialDto? Serial = null,
    ScannedDocumentDto? Document = null);

/// <summary>The structured fields decoded from the label, before any database lookup.</summary>
public record ScanPayloadDto(
    string Code,
    string Format,
    string? BatchNumber,
    string? SerialNumber,
    DateTime? ExpiryDate,
    int? Quantity)
{
    public static ScanPayloadDto From(BarcodePayload payload) => new(
        payload.Code,
        payload.Format.ToString(),
        payload.BatchNumber,
        payload.SerialNumber,
        payload.ExpiryDate,
        payload.Quantity);
}

public record ScannedProductDto(
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
    bool TrackBatch,
    bool TrackSerial,
    bool IsActive,
    /// <summary>Set when the scan matched a variant rather than the parent product.</summary>
    Guid? VariantId,
    string? VariantName,
    int TotalOnHand,
    int TotalAvailable,
    IReadOnlyList<ScannedStockDto> Stock,
    IReadOnlyList<ScannedSerialDto> Serials);

/// <summary>One inventory balance row behind a scanned product.</summary>
public record ScannedStockDto(
    Guid BalanceId,
    Guid WarehouseId,
    string WarehouseName,
    Guid? LocationId,
    string? LocationName,
    string? LocationCode,
    string? BatchNumber,
    DateTime? ExpiryDate,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    decimal UnitCost);

public record ScannedSerialDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string SerialNumber,
    string Status,
    Guid? WarehouseId,
    string? WarehouseName,
    Guid? LocationId,
    string? LocationName,
    string? BatchNumber,
    DateTime? ExpiryDate);

public record ScannedLocationDto(
    Guid Id,
    string Name,
    string? Code,
    string? Barcode,
    Guid WarehouseId,
    string WarehouseName,
    string? Aisle,
    string? Rack,
    string? Bin,
    bool IsActive);

public record ScannedWarehouseDto(
    Guid Id,
    string Name,
    string Code,
    bool IsActive);

/// <summary>A scanned sales or purchase order number.</summary>
public record ScannedDocumentDto(
    Guid Id,
    string DocumentType,
    string Number,
    string Status,
    Guid WarehouseId,
    string WarehouseName,
    string PartyName,
    DateTime OrderDate,
    int LineCount);

/// <summary>Stock available for a specific product/warehouse/location/batch combination.</summary>
public record ScanAvailabilityDto(
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid WarehouseId,
    string WarehouseName,
    Guid? LocationId,
    string? LocationName,
    string? BatchNumber,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    decimal UnitCost);
