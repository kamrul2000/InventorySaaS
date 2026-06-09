using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Billing;

/// <summary>A vendor bill (Accounts Payable). Paid down via supplier payments.</summary>
public class SupplierBill : TenantEntity
{
    public string BillNumber { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public DateTime BillDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public BillStatus Status { get; set; } = BillStatus.Draft;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? Notes { get; set; }

    public decimal BalanceDue => TotalAmount - AmountPaid;

    public Supplier.SupplierInfo Supplier { get; set; } = default!;
    public Purchase.PurchaseOrder? PurchaseOrder { get; set; }
    public ICollection<SupplierBillItem> Items { get; set; } = [];

    /// <summary>
    /// Applies a payment amount and advances the status (Open → PartiallyPaid → Paid).
    /// Draft/Cancelled bills are not eligible and must be guarded by the caller.
    /// </summary>
    public void ApplyPayment(decimal amount)
    {
        AmountPaid += amount;

        if (AmountPaid >= TotalAmount && TotalAmount > 0)
            Status = BillStatus.Paid;
        else if (AmountPaid > 0)
            Status = BillStatus.PartiallyPaid;
    }

    /// <summary>Reverses a previously applied payment amount (e.g. when a payment is voided).</summary>
    public void ReversePayment(decimal amount)
    {
        AmountPaid -= amount;
        if (AmountPaid < 0) AmountPaid = 0;

        Status = AmountPaid <= 0
            ? BillStatus.Open
            : BillStatus.PartiallyPaid;
    }
}
