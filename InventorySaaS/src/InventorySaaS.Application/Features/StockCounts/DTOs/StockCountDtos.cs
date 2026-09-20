namespace InventorySaaS.Application.Features.StockCounts.DTOs;

/// <summary>
/// A counting run and its variances. Counting never moves stock — approval does, by writing
/// one adjustment transaction per line that differs from the system quantity.
/// </summary>
public record StockCountSessionDto(
    Guid Id,
    string CountNumber,
    Guid WarehouseId,
    string WarehouseName,
    Guid? LocationId,
    string? LocationName,
    string Status,
    DateTime StartedAt,
    DateTime? SubmittedAt,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    string? Notes,
    int LineCount,
    int ShortageLines,
    int OverageLines,
    /// <summary>Net units across all variances: negative is a net shortage.</summary>
    int NetVariance,
    IReadOnlyList<StockCountLineDto> Lines);

public record StockCountLineDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string? Barcode,
    Guid? LocationId,
    string? LocationName,
    string? BatchNumber,
    bool TrackSerial,
    /// <summary>What the system believed when the line was first opened.</summary>
    int SystemQuantity,
    int CountedQuantity,
    int Variance,
    string? Notes,
    IReadOnlyList<string> Serials);

public record StartStockCountRequest(
    Guid WarehouseId,
    Guid? LocationId,
    string? Notes);

/// <summary>
/// One counting scan. Repeat scans of the same product, bin and batch add to that line's count.
/// Setting <paramref name="SetExactQuantity"/> replaces the running total instead, which is what
/// a typed correction needs.
/// </summary>
public record StockCountScanRequest(
    Guid? ProductId,
    string? Barcode,
    int Quantity = 1,
    Guid? LocationId = null,
    string? BatchNumber = null,
    string? SerialNumber = null,
    bool SetExactQuantity = false,
    string? Notes = null);

public record SubmitStockCountRequest(string? Notes);

/// <summary>
/// Manager sign-off. Approving writes the adjustments; the count is the record of why.
/// </summary>
public record ApproveStockCountRequest(string? Notes);

public record StockCountScanResultDto(
    bool Accepted,
    string Message,
    StockCountLineDto? Line,
    StockCountSessionDto Session);
