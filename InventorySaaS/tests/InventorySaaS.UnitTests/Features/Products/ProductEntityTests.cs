using FluentAssertions;
using InventorySaaS.Domain.Entities.Product;

namespace InventorySaaS.UnitTests.Features.Products;

public class ProductEntityTests
{
    [Fact]
    public void ProductInfo_ShouldInitializeWithDefaults()
    {
        var product = new ProductInfo();

        product.Id.Should().NotBeEmpty();
        product.IsDeleted.Should().BeFalse();
        product.IsActive.Should().BeTrue();
        product.MinimumOrderQuantity.Should().Be(1);
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Category_ShouldSupportHierarchy()
    {
        var parent = new Category { Name = "Electronics" };
        var child = new Category { Name = "Phones", ParentCategoryId = parent.Id };

        child.ParentCategoryId.Should().Be(parent.Id);
    }

    [Fact]
    public void ProductVariant_ShouldLinkToProduct()
    {
        var product = new ProductInfo { Name = "T-Shirt", Sku = "TSH-001" };
        var variant = new ProductVariant
        {
            ProductId = product.Id,
            Name = "Large - Blue",
            Sku = "TSH-001-LG-BL"
        };

        variant.ProductId.Should().Be(product.Id);
    }
}
