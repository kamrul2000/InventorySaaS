using FluentAssertions;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Persistence;

/// <summary>
/// Regression tests for the global tenant-isolation query filter on <see cref="ApplicationDbContext"/>.
/// Guards against the prior bug where the filter was effectively "WHERE TenantId == Empty OR true"
/// (i.e. disabled), allowing cross-tenant data leakage.
/// </summary>
public class TenantIsolationTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId { get; set; }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => null;
        public Guid? TenantId { get; set; }
        public bool IsSuperAdmin => false;
        public IReadOnlyList<string> Roles => [];
    }

    private static ApplicationDbContext CreateContext(string dbName, Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(
            options,
            new FakeTenantAccessor { TenantId = tenantId },
            new FakeCurrentUserService { TenantId = tenantId });
    }

    private static ProductInfo NewProduct(Guid tenantId, string sku) => new()
    {
        TenantId = tenantId,
        Name = $"Product {sku}",
        Sku = sku,
        CategoryId = Guid.NewGuid(),
        UnitOfMeasureId = Guid.NewGuid()
    };

    [Fact]
    public async Task Query_ShouldOnlyReturnRecordsForCurrentTenant()
    {
        var dbName = Guid.NewGuid().ToString();

        // Seed both tenants using a system context (null tenant => no scoping on write).
        using (var seed = CreateContext(dbName, tenantId: null))
        {
            seed.Products.Add(NewProduct(TenantA, "A-1"));
            seed.Products.Add(NewProduct(TenantA, "A-2"));
            seed.Products.Add(NewProduct(TenantB, "B-1"));
            await seed.SaveChangesAsync();
        }

        // Tenant B must only ever see its own product.
        using (var tenantBContext = CreateContext(dbName, TenantB))
        {
            var products = await tenantBContext.Products.ToListAsync();

            products.Should().HaveCount(1);
            products.Should().OnlyContain(p => p.TenantId == TenantB);
            products[0].Sku.Should().Be("B-1");
        }
    }

    [Fact]
    public async Task NullTenant_ShouldBypassFilter_ForSystemOperations()
    {
        var dbName = Guid.NewGuid().ToString();

        using (var seed = CreateContext(dbName, tenantId: null))
        {
            seed.Products.Add(NewProduct(TenantA, "A-1"));
            seed.Products.Add(NewProduct(TenantB, "B-1"));
            await seed.SaveChangesAsync();
        }

        // A null-tenant (seeding / SuperAdmin) context sees everything.
        using (var system = CreateContext(dbName, tenantId: null))
        {
            var products = await system.Products.ToListAsync();
            products.Should().HaveCount(2);
        }
    }

    [Fact]
    public async Task SoftDeletedRecords_ShouldBeExcluded_WithinTenant()
    {
        var dbName = Guid.NewGuid().ToString();

        using (var seed = CreateContext(dbName, tenantId: null))
        {
            var active = NewProduct(TenantA, "A-ACTIVE");
            var deleted = NewProduct(TenantA, "A-DELETED");
            deleted.IsDeleted = true;
            seed.Products.AddRange(active, deleted);
            await seed.SaveChangesAsync();
        }

        using (var tenantAContext = CreateContext(dbName, TenantA))
        {
            var products = await tenantAContext.Products.ToListAsync();

            products.Should().HaveCount(1);
            products[0].Sku.Should().Be("A-ACTIVE");
        }
    }
}
