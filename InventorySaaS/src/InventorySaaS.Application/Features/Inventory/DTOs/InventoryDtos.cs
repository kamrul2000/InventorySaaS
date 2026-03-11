namespace InventorySaaS.Application.Features.Inventory.DTOs;

public record InventoryBalanceDto(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductSku,
    Guid WarehouseId,
    string WarehouseName,
    string? LocationName,
    string? BatchNumber,
    DateTime? ExpiryDate,
    int QuantityOnHand,
    int QuantityReserved,
    int QuantityAvailable,
    decimal UnitCost);

public record StockInRequest(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    decimal UnitCost,
    string? BatchNumber,
    string? LotNumber,
    DateTime? ExpiryDate,
    string? Notes);

public record StockOutRequest(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    string? Notes);

public record StockTransferRequest(
    Guid ProductId,
    Guid SourceWarehouseId,
    Guid? SourceLocationId,
    Guid DestinationWarehouseId,
    Guid? DestinationLocationId,
    int Quantity,
    string? Notes);

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
    string? Notes);
