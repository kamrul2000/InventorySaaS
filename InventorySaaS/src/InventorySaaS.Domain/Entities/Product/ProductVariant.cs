using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Product;

public class ProductVariant : TenantEntity
{
    public Guid ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string Sku { get; set; } = default!;
    public string? Barcode { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public bool IsActive { get; set; } = true;
    public string? AttributesJson { get; set; }

    public ProductInfo Product { get; set; } = default!;
}
