namespace InventorySaaS.Application.Features.Dashboard.DTOs;

public record DashboardDto(
    int TotalProducts,
    int TotalWarehouses,
    int TotalSuppliers,
    int TotalCustomers,
    int LowStockCount,
    int ExpiringCount,
    decimal TotalInventoryValue,
    List<RecentTransactionDto> RecentTransactions,
    List<TopProductDto> TopProducts,
    List<StockAlertDto> StockAlerts);

public record RecentTransactionDto(
    string TransactionNumber,
    string Type,
    string ProductName,
    int Quantity,
    DateTime Date);

public record TopProductDto(
    string ProductName,
    string Sku,
    int TotalQuantity,
    decimal TotalValue);

public record StockAlertDto(
    string ProductName,
    string Sku,
    string WarehouseName,
    int CurrentStock,
    int ReorderLevel);
