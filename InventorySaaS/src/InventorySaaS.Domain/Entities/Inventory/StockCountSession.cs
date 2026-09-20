using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Inventory;

/// <summary>
/// A physical stock-count run over one warehouse (optionally narrowed to a single location).
/// Counting itself never touches inventory: lines accumulate scanned quantities, and only a
/// Manager approval converts the variances into <see cref="InventoryTransaction"/> adjustments.
/// </summary>
public class StockCountSession : TenantEntity
{
    public string CountNumber { get; set; } = default!;
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public StockCountStatus Status { get; set; } = StockCountStatus.Counting;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Notes { get; set; }

    public Warehouse.WarehouseInfo Warehouse { get; set; } = default!;
    public Warehouse.WarehouseLocation? Location { get; set; }
    public ICollection<StockCountLine> Lines { get; set; } = [];
}

/// <summary>
/// One product/location/batch combination within a count. <see cref="SystemQuantity"/> is
/// snapshotted when the line is first created so the variance reflects what the system believed
/// at the moment counting began, not what it believes at approval time.
/// </summary>
public class StockCountLine : TenantEntity
{
    public Guid SessionId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public string? BatchNumber { get; set; }

    public int SystemQuantity { get; set; }
    public int CountedQuantity { get; set; }

    /// <summary>Positive is an overage, negative a shortage.</summary>
    public int Variance => CountedQuantity - SystemQuantity;

    public string? Notes { get; set; }

    public StockCountSession Session { get; set; } = default!;
    public Product.ProductInfo Product { get; set; } = default!;
    public Warehouse.WarehouseLocation? Location { get; set; }
    public ICollection<StockCountLineSerial> Serials { get; set; } = [];
}

/// <summary>A single serial number sighted while counting a serial-controlled product.</summary>
public class StockCountLineSerial : TenantEntity
{
    public Guid LineId { get; set; }
    public string SerialNumber { get; set; } = default!;

    public StockCountLine Line { get; set; } = default!;
}
