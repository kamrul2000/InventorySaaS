using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Inventory;

public class InventoryBalance : TenantEntity
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public string? BatchNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int QuantityOnHand { get; set; }
    public int QuantityReserved { get; set; }
    public int QuantityAvailable => QuantityOnHand - QuantityReserved;
    public decimal UnitCost { get; set; }

    public Product.ProductInfo Product { get; set; } = default!;
    public Warehouse.WarehouseInfo Warehouse { get; set; } = default!;
    public Warehouse.WarehouseLocation? Location { get; set; }
}
