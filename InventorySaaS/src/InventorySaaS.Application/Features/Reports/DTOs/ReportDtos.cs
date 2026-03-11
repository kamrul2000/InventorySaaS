namespace InventorySaaS.Application.Features.Reports.DTOs;

public record StockSummaryReportDto(
    string ProductName,
    string Sku,
    string CategoryName,
    string WarehouseName,
    int QuantityOnHand,
    decimal UnitCost,
    decimal TotalValue);

public record LowStockReportDto(
    string ProductName,
    string Sku,
    string WarehouseName,
    int CurrentStock,
    int ReorderLevel,
    int Deficit);

public record ExpiryReportDto(
    string ProductName,
    string Sku,
    string WarehouseName,
    string? BatchNumber,
    DateTime ExpiryDate,
    int Quantity,
    int DaysUntilExpiry);

public record InventoryValuationDto(
    string CategoryName,
    int ProductCount,
    decimal TotalCostValue,
    decimal TotalSellingValue);
