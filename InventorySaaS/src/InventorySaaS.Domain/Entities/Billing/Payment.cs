using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Billing;

/// <summary>A customer receipt (Accounts Receivable). Allocated across one or more invoices.</summary>
public class Payment : TenantEntity
{
    public string PaymentNumber { get; set; } = default!;
    public Guid CustomerId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public Customer.CustomerInfo Customer { get; set; } = default!;
    public ICollection<PaymentAllocation> Allocations { get; set; } = [];
}
