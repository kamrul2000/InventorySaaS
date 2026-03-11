using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Customer;

public class CustomerInfo : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? CustomerType { get; set; }
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? TaxId { get; set; }
    public string? PaymentTerms { get; set; }
    public decimal? CreditLimit { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Sales.SalesOrder> SalesOrders { get; set; } = [];
}
