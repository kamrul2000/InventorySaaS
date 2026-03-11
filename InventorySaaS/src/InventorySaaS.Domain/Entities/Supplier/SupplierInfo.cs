using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Supplier;

public class SupplierInfo : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public string? Website { get; set; }
    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public decimal? Rating { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Purchase.PurchaseOrder> PurchaseOrders { get; set; } = [];
}
