using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Product;

public class Category : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public Category? ParentCategory { get; set; }
    public ICollection<Category> SubCategories { get; set; } = [];
    public ICollection<ProductInfo> Products { get; set; } = [];
}
