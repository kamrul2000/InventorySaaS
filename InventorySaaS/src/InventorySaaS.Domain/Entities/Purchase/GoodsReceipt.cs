using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Purchase;

public class GoodsReceipt : TenantEntity
{
    public string ReceiptNumber { get; set; } = default!;
    public Guid PurchaseOrderId { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = default!;
    public ICollection<GoodsReceiptItem> Items { get; set; } = [];
}

public class GoodsReceiptItem : TenantEntity
{
    public Guid GoodsReceiptId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? LocationId { get; set; }
    public int Quantity { get; set; }
    public int RejectedQuantity { get; set; }
    public string? BatchNumber { get; set; }
    public string? LotNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = default!;
    public Product.ProductInfo Product { get; set; } = default!;
}
