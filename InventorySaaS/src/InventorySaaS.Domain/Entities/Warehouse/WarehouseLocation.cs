using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Warehouse;

public class WarehouseLocation : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>Short human-readable code for the bin, unique per tenant when set.</summary>
    public string? Code { get; set; }

    /// <summary>
    /// The value printed on the physical location label. Unique per tenant when set, so a scan
    /// resolves to exactly one bin.
    /// </summary>
    public string? Barcode { get; set; }
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Bin { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public WarehouseInfo Warehouse { get; set; } = default!;
    public ICollection<Inventory.InventoryBalance> InventoryBalances { get; set; } = [];
}
