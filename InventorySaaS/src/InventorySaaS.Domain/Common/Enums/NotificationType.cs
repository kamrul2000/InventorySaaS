namespace InventorySaaS.Domain.Common.Enums;

public enum NotificationType
{
    LowStock = 0,
    ExpiryAlert = 1,
    PurchaseOrderCreated = 2,
    SalesOrderCreated = 3,
    StockTransfer = 4,
    SystemAlert = 5,
    UserInvitation = 6
}
