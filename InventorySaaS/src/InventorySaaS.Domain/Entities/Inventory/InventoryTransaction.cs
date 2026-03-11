using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Inventory;

public class InventoryTransaction : TenantEntity
{
    public string TransactionNumber { get; set; } = default!;
    public TransactionType TransactionType { get; set; }
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? DestinationWarehouseId { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? BatchNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? Notes { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

    public Product.ProductInfo Product { get; set; } = default!;
    public Warehouse.WarehouseInfo Warehouse { get; set; } = default!;
}
