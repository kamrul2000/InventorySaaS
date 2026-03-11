using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Product;

public class ProductImage : TenantEntity
{
    public Guid ProductId { get; set; }
    public string FileName { get; set; } = default!;
    public string Url { get; set; } = default!;
    public bool IsPrimary { get; set; }
    public int SortOrder { get; set; }

    public ProductInfo Product { get; set; } = default!;
}
