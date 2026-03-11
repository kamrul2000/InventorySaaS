using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Warehouse;

public class WarehouseLocation : TenantEntity
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = default!;
    public string? Aisle { get; set; }
    public string? Rack { get; set; }
    public string? Bin { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public WarehouseInfo Warehouse { get; set; } = default!;
    public ICollection<Inventory.InventoryBalance> InventoryBalances { get; set; } = [];
}
