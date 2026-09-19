namespace InventorySaaS.Application.Features.Picking.DTOs;

/// <summary>
/// A picking run and its progress. <c>Lines</c> always covers every line on the order, so the
/// screen can show what is left to pick as well as what has been collected.
/// </summary>
public record PickSessionDto(
    Guid Id,
    Guid SalesOrderId,
    string OrderNumber,
    string OrderStatus,
    string CustomerName,
    Guid WarehouseId,
    string WarehouseName,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? PickedBy,
    string? Notes,
    int TotalRequested,
    int TotalPicked,
    int TotalRemaining,
    bool IsFullyPicked,
    IReadOnlyList<PickLineDto> Lines);

public record PickLineDto(
    Guid SalesOrderItemId,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    string? Barcode,
    bool TrackBatch,
    bool TrackSerial,
    int RequestedQuantity,
    int PickedQuantity,
    int RemainingQuantity,
    int DeliveredQuantity);

public record StartPickRequest(string? Notes);

/// <summary>
/// One pick. The product may be named directly or identified by
/// <paramref name="Barcode"/>, which is what the scanner sends.
/// </summary>
public record PickScanRequest(
    Guid? ProductId,
    string? Barcode,
    int Quantity = 1,
    string? BatchNumber = null,
    string? SerialNumber = null);

/// <summary>
/// Closes the session. <paramref name="Deliver"/> hands the picked quantities to the existing
/// delivery path, which is what actually draws the stock down.
/// </summary>
public record CompletePickRequest(bool Deliver = false, string? Notes = null);

/// <summary>The result of a scan during picking, including the line it was applied to.</summary>
public record PickScanResultDto(
    bool Accepted,
    string Message,
    PickLineDto? Line,
    PickSessionDto Session);
