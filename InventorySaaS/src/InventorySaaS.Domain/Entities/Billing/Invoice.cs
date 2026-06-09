using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Billing;

public class Invoice : TenantEntity
{
    public string InvoiceNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public string? Notes { get; set; }

    public decimal BalanceDue => TotalAmount - AmountPaid;

    public Customer.CustomerInfo Customer { get; set; } = default!;
    public Sales.SalesOrder? SalesOrder { get; set; }
    public ICollection<InvoiceItem> Items { get; set; } = [];

    /// <summary>
    /// Applies a payment amount and advances the status (Issued → PartiallyPaid → Paid).
    /// Draft/Cancelled invoices are not eligible and must be guarded by the caller.
    /// </summary>
    public void ApplyPayment(decimal amount)
    {
        AmountPaid += amount;

        if (AmountPaid >= TotalAmount && TotalAmount > 0)
            Status = InvoiceStatus.Paid;
        else if (AmountPaid > 0)
            Status = InvoiceStatus.PartiallyPaid;
    }

    /// <summary>Reverses a previously applied payment amount (e.g. when a receipt is voided).</summary>
    public void ReversePayment(decimal amount)
    {
        AmountPaid -= amount;
        if (AmountPaid < 0) AmountPaid = 0;

        Status = AmountPaid <= 0
            ? InvoiceStatus.Issued
            : InvoiceStatus.PartiallyPaid;
    }
}
