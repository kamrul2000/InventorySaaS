namespace InventorySaaS.Application.Features.Inventory.DTOs;

public record InventoryBalanceDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid WarehouseId,
    string WarehouseName,
    Guid? LocationId,
    string? LocationName,
    string? BatchNumber,
    DateTime? ExpiryDate,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    decimal UnitCost);

/// <summary>
/// Receives stock. <paramref name="SerialNumbers"/> is required, and must have exactly
/// <paramref name="Quantity"/> entries, when the product is serial-tracked.
/// </summary>
public record StockInRequest(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    decimal UnitCost,
    string? BatchNumber,
    string? LotNumber,
    DateTime? ExpiryDate,
    string? Notes,
    IReadOnlyList<string>? SerialNumbers = null);

/// <summary>
/// Issues stock. <paramref name="BatchNumber"/> is required for batch-tracked products so the
/// batch is preserved on the way out; for untracked products, omitting it draws earliest-expiry-first.
/// </summary>
public record StockOutRequest(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    string? Notes,
    string? Reason = null,
    string? BatchNumber = null,
    IReadOnlyList<string>? SerialNumbers = null);

public record StockTransferRequest(
    Guid ProductId,
    Guid SourceWarehouseId,
    Guid? SourceLocationId,
    Guid DestinationWarehouseId,
    Guid? DestinationLocationId,
    int Quantity,
    string? Notes,
    string? BatchNumber = null,
    IReadOnlyList<string>? SerialNumbers = null);

public record StockAdjustmentRequest(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int NewQuantity,
    string Reason);

public record InventoryTransactionDto(
    Guid Id,
    string TransactionNumber,
    string TransactionType,
    string ProductName,
    string ProductSku,
    string WarehouseName,
    int Quantity,
    decimal UnitCost,
    string? BatchNumber,
    DateTime TransactionDate,
    string? Notes,
    string? Reason = null,
    IReadOnlyList<string>? SerialNumbers = null);
