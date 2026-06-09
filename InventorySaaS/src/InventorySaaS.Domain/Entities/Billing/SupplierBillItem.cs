using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Billing;

public class SupplierBillItem : TenantEntity
{
    public Guid SupplierBillId { get; set; }
    public Guid? ProductId { get; set; }
    public string Description { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal LineTotal { get; set; }

    public SupplierBill SupplierBill { get; set; } = default!;
    public Product.ProductInfo? Product { get; set; }
}
