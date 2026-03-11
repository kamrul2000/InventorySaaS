using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Purchase;

public class PurchaseOrderItem : TenantEntity
{
    public Guid PurchaseOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public int ReceivedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = default!;
    public Product.ProductInfo Product { get; set; } = default!;
}
