using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Sales;

public class SalesOrderItem : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public int DeliveredQuantity { get; set; }

    /// <summary>
    /// How much of this line warehouse staff have physically picked. Picking does not move
    /// stock — delivery does — so this only ever runs ahead of <see cref="DeliveredQuantity"/>.
    /// </summary>
    public int PickedQuantity { get; set; }
    public int ReturnedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal DiscountRate { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }

    public SalesOrder SalesOrder { get; set; } = default!;
    public Product.ProductInfo Product { get; set; } = default!;
}
