using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Billing;

/// <summary>Links a portion of a <see cref="Payment"/> to a specific <see cref="Invoice"/>.</summary>
public class PaymentAllocation : TenantEntity
{
    public Guid PaymentId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }

    public Payment Payment { get; set; } = default!;
    public Invoice Invoice { get; set; } = default!;
}
