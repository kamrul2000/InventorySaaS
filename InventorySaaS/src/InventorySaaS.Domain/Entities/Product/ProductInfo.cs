using InventorySaaS.Domain.Common;

namespace InventorySaaS.Domain.Entities.Product;

public class ProductInfo : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Sku { get; set; } = default!;
    public string? Barcode { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int ReorderLevel { get; set; }
    public int MinimumOrderQuantity { get; set; } = 1;
    public bool TrackExpiry { get; set; }
    public bool HasVariants { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? Weight { get; set; }
    public string? Dimensions { get; set; }
    public string? Notes { get; set; }
    public string? Tags { get; set; }

    public Category Category { get; set; } = default!;
    public Brand? Brand { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; } = default!;
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<Inventory.InventoryBalance> InventoryBalances { get; set; } = [];
}
