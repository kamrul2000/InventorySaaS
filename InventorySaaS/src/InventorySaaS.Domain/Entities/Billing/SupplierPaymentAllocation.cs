using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Billing;

/// <summary>Links a portion of a <see cref="SupplierPayment"/> to a specific <see cref="SupplierBill"/>.</summary>
public class SupplierPaymentAllocation : TenantEntity
{
    public Guid SupplierPaymentId { get; set; }
    public Guid SupplierBillId { get; set; }
    public decimal Amount { get; set; }

    public SupplierPayment SupplierPayment { get; set; } = default!;
    public SupplierBill SupplierBill { get; set; } = default!;
}
