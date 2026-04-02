namespace InventorySaaS.Application.Features.Dashboard.DTOs;

public record DashboardDto(
    int TotalProducts,
    int TotalWarehouses,
    int TotalSuppliers,
    int TotalCustomers,
    int LowStockCount,
    int ExpiringCount,
    decimal TotalInventoryValue,
    decimal TotalSales,
    decimal TotalPurchases,
    int TotalOrders,
    List<RecentTransactionDto> RecentTransactions,
    List<TopProductDto> TopProducts,
    List<StockAlertDto> StockAlerts,
    List<RecentSalesOrderDto> RecentSalesOrders,
    List<LowStockProductDto> LowStockProducts);

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
    decimal TotalValue,
    decimal SellingPrice);

public record StockAlertDto(
    string ProductName,
    string Sku,
    string WarehouseName,
    int CurrentStock,
    int ReorderLevel);

public record RecentSalesOrderDto(
    string OrderNumber,
    string CustomerName,
    string Status,
    decimal TotalAmount,
    DateTime OrderDate);

public record LowStockProductDto(
    string ProductName,
    string Sku,
    int CurrentStock,
    int ReorderLevel);
