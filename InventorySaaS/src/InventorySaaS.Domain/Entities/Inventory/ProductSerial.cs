using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Inventory;

/// <summary>
/// One individually tracked unit of a serial-controlled product
/// (<see cref="Product.ProductInfo.TrackSerial"/>).
///
/// Serial numbers are unique per tenant, enforced by a filtered unique index rather than by
/// service-level checks alone, so a race between two concurrent scans cannot create a duplicate.
/// A serial carries its own location and batch so that receiving, issuing and transferring a
/// serial-controlled product keeps batch and expiry attached to the individual unit.
/// </summary>
public class ProductSerial : TenantEntity
{
    public Guid ProductId { get; set; }
    public string SerialNumber { get; set; } = default!;
    public SerialStatus Status { get; set; } = SerialStatus.InStock;

    public Guid? WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public string? BatchNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public decimal UnitCost { get; set; }

    /// <summary>The inventory transaction that last moved this unit — the audit trail back-pointer.</summary>
    public Guid? LastTransactionId { get; set; }
    public string? Notes { get; set; }

    public Product.ProductInfo Product { get; set; } = default!;
    public Warehouse.WarehouseInfo? Warehouse { get; set; }
    public Warehouse.WarehouseLocation? Location { get; set; }
}
