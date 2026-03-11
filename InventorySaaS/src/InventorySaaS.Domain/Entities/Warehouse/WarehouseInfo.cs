using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Warehouse;

public class WarehouseInfo : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<WarehouseLocation> Locations { get; set; } = [];
    public ICollection<Inventory.InventoryBalance> InventoryBalances { get; set; } = [];
}
