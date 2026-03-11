using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Product;

public class UnitOfMeasure : TenantEntity
{
    public string Name { get; set; } = default!;
    public string Abbreviation { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public ICollection<ProductInfo> Products { get; set; } = [];
}
