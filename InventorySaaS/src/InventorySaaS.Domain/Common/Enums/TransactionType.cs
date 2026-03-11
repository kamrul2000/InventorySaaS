namespace InventorySaaS.Domain.Common.Enums;

public enum TransactionType
{
    StockIn = 0,
    StockOut = 1,
    Transfer = 2,
    Adjustment = 3,
    Return = 4,
    Damaged = 5,
    Lost = 6,
    PurchaseReceive = 7,
    SalesIssue = 8
}
