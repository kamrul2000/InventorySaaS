namespace InventorySaaS.Domain.Common.Enums;

/// <summary>Lifecycle of an individually tracked unit of a serial-controlled product.</summary>
public enum SerialStatus
{
    InStock = 0,
    Reserved = 1,
    Issued = 2,
    Scrapped = 3
}

/// <summary>
/// Lifecycle of a physical stock-count session. Counting is Staff work; crossing from
/// PendingApproval to Approved is the Manager gate that turns variances into adjustments.
/// </summary>
public enum StockCountStatus
{
    Counting = 0,
    PendingApproval = 1,
    Approved = 2,
    Cancelled = 3
}

/// <summary>Lifecycle of a sales-order picking session.</summary>
public enum PickSessionStatus
{
    Open = 0,
    Completed = 1,
    Cancelled = 2
}

/// <summary>
/// What a scanned string turned out to be. <see cref="Unknown"/> is a first-class result:
/// the caller shows "not recognised" rather than an error.
/// </summary>
public enum ScanKind
{
    Unknown = 0,
    Product = 1,
    ProductVariant = 2,
    Serial = 3,
    Location = 4,
    Warehouse = 5,
    SalesOrder = 6,
    PurchaseOrder = 7
}
