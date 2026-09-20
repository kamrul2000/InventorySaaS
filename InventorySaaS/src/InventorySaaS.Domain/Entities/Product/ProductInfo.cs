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

    /// <summary>
    /// When set, stock movements must carry the batch number through, and a balance is only
    /// matched on an exact batch. Off by default so existing products behave exactly as before.
    /// </summary>
    public bool TrackBatch { get; set; }

    /// <summary>
    /// When set, every unit is recorded individually as a <see cref="Inventory.ProductSerial"/>
    /// and movements must name the serial numbers involved.
    /// </summary>
    public bool TrackSerial { get; set; }
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
    public ICollection<Inventory.ProductSerial> Serials { get; set; } = [];
}
