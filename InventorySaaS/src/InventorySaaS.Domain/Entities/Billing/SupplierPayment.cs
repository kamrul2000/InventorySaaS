using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Billing;

/// <summary>A payment made to a supplier (Accounts Payable). Allocated across one or more bills.</summary>
public class SupplierPayment : TenantEntity
{
    public string PaymentNumber { get; set; } = default!;
    public Guid SupplierId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public Supplier.SupplierInfo Supplier { get; set; } = default!;
    public ICollection<SupplierPaymentAllocation> Allocations { get; set; } = [];
}
