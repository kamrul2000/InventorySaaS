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

/// <summary>
/// One party's outstanding balance split by how long each document has been overdue.
/// Used for both receivables (customers) and payables (suppliers) — the buckets are the same.
/// </summary>
public record AgingReportDto(
    string PartyName,
    /// <summary>Not yet due.</summary>
    decimal Current,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days90Plus,
    decimal Total,
    int DocumentCount,
    /// <summary>Age of the oldest unpaid document, for spotting the worst offender at a glance.</summary>
    int OldestDaysOverdue);

public record SalesSummaryDto(
    string CustomerName,
    int OrderCount,
    int UnitsSold,
    decimal SubTotal,
    decimal TaxAmount,
    decimal DiscountAmount,
    decimal TotalAmount);

public record PurchaseSummaryDto(
    string SupplierName,
    int OrderCount,
    int UnitsOrdered,
    int UnitsReceived,
    decimal TotalAmount);

/// <summary>
/// Margin per product over a period. Cost comes from the stock actually drawn down at delivery
/// (recorded on the inventory ledger), not the catalogue cost price, so it reflects real COGS.
/// </summary>
public record ProfitabilityDto(
    string ProductName,
    string Sku,
    string CategoryName,
    int UnitsSold,
    decimal Revenue,
    decimal Cost,
    decimal GrossProfit,
    decimal MarginPercent);
