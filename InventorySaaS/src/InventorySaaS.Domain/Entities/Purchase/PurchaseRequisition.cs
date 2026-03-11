using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Purchase;

public class PurchaseRequisition : TenantEntity
{
    public string RequisitionNumber { get; set; } = default!;
    public DateTime RequisitionDate { get; set; } = DateTime.UtcNow;
    public RequisitionStatus Status { get; set; } = RequisitionStatus.Draft;
    public string? RequestedBy { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseRequisitionItem> Items { get; set; } = [];
}

public class PurchaseRequisitionItem : TenantEntity
{
    public Guid RequisitionId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal? EstimatedUnitPrice { get; set; }
    public string? Notes { get; set; }

    public PurchaseRequisition Requisition { get; set; } = default!;
    public Product.ProductInfo Product { get; set; } = default!;
}
