using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Product;

public class Brand : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ProductInfo> Products { get; set; } = [];
}
