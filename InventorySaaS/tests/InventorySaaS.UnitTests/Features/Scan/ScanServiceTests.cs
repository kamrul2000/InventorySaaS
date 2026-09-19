using FluentAssertions;
using InventorySaaS.Application.Features.Scan.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Scan;

/// <summary>
/// Covers barcode resolution against seeded tenant data, including the tenant-isolation
/// guarantee that a scan must never reach across tenants.
/// </summary>
public class ScanServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid CategoryId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UnitId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid WarehouseId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid LocationId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProductId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid SerialProductId = Guid.Parse("88888888-8888-8888-8888-888888888888");

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

    private static ApplicationDbContext CreateContext(string dbName, Guid? tenantId) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options,
            new FakeTenantAccessor { TenantId = tenantId },
            new FakeCurrentUserService { TenantId = tenantId });

    /// <summary>
    /// Seeds a warehouse, a bin, a plain product, a serial-tracked product and a sales order for
    /// tenant A, plus a same-barcode product for tenant B to prove isolation.
    /// </summary>
    private static string SeedDatabase()
    {
        var dbName = Guid.NewGuid().ToString();

        // A null tenant bypasses write scoping, which is how the seeder reaches both tenants.
        using var db = CreateContext(dbName, tenantId: null);

        db.Categories.Add(new Category { Id = CategoryId, TenantId = TenantA, Name = "Tools" });
        db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = UnitId, TenantId = TenantA, Name = "Piece", Abbreviation = "pc" });

        db.Warehouses.Add(new WarehouseInfo
        {
            Id = WarehouseId, TenantId = TenantA, Name = "Main Warehouse", Code = "WH-MAIN"
        });

        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = LocationId, TenantId = TenantA, WarehouseId = WarehouseId,
            Name = "Aisle 1 / Bin 3", Code = "A1-B3", Barcode = "LOC-A1B3"
        });

        db.Products.Add(new ProductInfo
        {
            Id = ProductId, TenantId = TenantA, Name = "Hammer", Sku = "TOOL-00001",
            Barcode = "8801234567890", CategoryId = CategoryId, UnitOfMeasureId = UnitId,
            CostPrice = 10m, SellingPrice = 18m, TrackBatch = true
        });

        db.Products.Add(new ProductInfo
        {
            Id = SerialProductId, TenantId = TenantA, Name = "Cordless Drill", Sku = "TOOL-00002",
            Barcode = "8809999999999", CategoryId = CategoryId, UnitOfMeasureId = UnitId,
            CostPrice = 90m, SellingPrice = 140m, TrackSerial = true
        });

        db.ProductVariants.Add(new ProductVariant
        {
            TenantId = TenantA, ProductId = ProductId, Name = "Hammer 500g",
            Sku = "TOOL-00001-500", Barcode = "VAR-500"
        });

        db.ProductSerials.Add(new ProductSerial
        {
            TenantId = TenantA, ProductId = SerialProductId, SerialNumber = "SN-DRILL-001",
            Status = SerialStatus.InStock, WarehouseId = WarehouseId, LocationId = LocationId,
            UnitCost = 90m
        });

        // Two batches in the same bin, so batch-level breakdown is exercised.
        db.InventoryBalances.Add(new InventoryBalance
        {
            TenantId = TenantA, ProductId = ProductId, WarehouseId = WarehouseId, LocationId = LocationId,
            BatchNumber = "B-001", ExpiryDate = new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            QuantityOnHand = 40, QuantityReserved = 10, UnitCost = 10m
        });

        db.InventoryBalances.Add(new InventoryBalance
        {
            TenantId = TenantA, ProductId = ProductId, WarehouseId = WarehouseId, LocationId = LocationId,
            BatchNumber = "B-002", QuantityOnHand = 25, QuantityReserved = 0, UnitCost = 12m
        });

        var customerId = Guid.NewGuid();
        db.Customers.Add(new CustomerInfo { Id = customerId, TenantId = TenantA, Name = "Acme Ltd" });
        db.SalesOrders.Add(new SalesOrder
        {
            TenantId = TenantA, OrderNumber = "SO-2026-0001", CustomerId = customerId,
            WarehouseId = WarehouseId, Status = SalesOrderStatus.Confirmed
        });

        // Tenant B holds the same barcode; tenant A must never see it. It needs its own
        // category and unit because both navigations are required.
        var tenantBCategoryId = Guid.NewGuid();
        var tenantBUnitId = Guid.NewGuid();
        db.Categories.Add(new Category { Id = tenantBCategoryId, TenantId = TenantB, Name = "Tools" });
        db.UnitsOfMeasure.Add(new UnitOfMeasure
        {
            Id = tenantBUnitId, TenantId = TenantB, Name = "Piece", Abbreviation = "pc"
        });
        db.Products.Add(new ProductInfo
        {
            TenantId = TenantB, Name = "Other Tenant Hammer", Sku = "OTHER-1",
            Barcode = "8801234567890", CategoryId = tenantBCategoryId, UnitOfMeasureId = tenantBUnitId
        });

        db.SaveChangesAsync().GetAwaiter().GetResult();
        return dbName;
    }

    private static ScanService CreateService(string dbName, Guid tenantId) =>
        new(CreateContext(dbName, tenantId));

    [Fact]
    public async Task Resolve_ShouldMatchProductByBarcode()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("8801234567890"), default);

        result.Matched.Should().BeTrue();
        result.Kind.Should().Be("Product");
        result.Product!.Name.Should().Be("Hammer");
        result.Product.Sku.Should().Be("TOOL-00001");
    }

    [Fact]
    public async Task Resolve_ShouldMatchProductBySku_WhenBarcodeDoesNotMatch()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("TOOL-00001"), default);

        result.Kind.Should().Be("Product");
        result.Product!.Id.Should().Be(ProductId);
    }

    [Fact]
    public async Task Resolve_ShouldReportProductStockByWarehouseLocationAndBatch()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("8801234567890"), default);

        var product = result.Product!;
        product.TotalOnHand.Should().Be(65);
        product.TotalAvailable.Should().Be(55);
        product.Stock.Should().HaveCount(2);

        var firstBatch = product.Stock.Single(s => s.BatchNumber == "B-001");
        firstBatch.WarehouseName.Should().Be("Main Warehouse");
        firstBatch.LocationName.Should().Be("Aisle 1 / Bin 3");
        firstBatch.LocationCode.Should().Be("A1-B3");
        firstBatch.QuantityAvailable.Should().Be(30);
        firstBatch.ExpiryDate.Should().Be(new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Resolve_ShouldMatchVariantByBarcode()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("VAR-500"), default);

        result.Kind.Should().Be("ProductVariant");
        result.Product!.VariantName.Should().Be("Hammer 500g");
        // The variant resolves to its parent product, so stock is still reachable.
        result.Product.Id.Should().Be(ProductId);
    }

    [Fact]
    public async Task Resolve_ShouldMatchSerialNumber()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("SN-DRILL-001"), default);

        result.Kind.Should().Be("Serial");
        result.Serial!.ProductSku.Should().Be("TOOL-00002");
        result.Serial.Status.Should().Be("InStock");
        result.Serial.WarehouseName.Should().Be("Main Warehouse");
    }

    [Fact]
    public async Task Resolve_ShouldMatchWarehouseLocation_ByBarcodeAndByCode()
    {
        var dbName = SeedDatabase();

        var byBarcode = await CreateService(dbName, TenantA).ResolveAsync(new ResolveScanRequest("LOC-A1B3"), default);
        byBarcode.Kind.Should().Be("Location");
        byBarcode.Location!.WarehouseName.Should().Be("Main Warehouse");

        var byCode = await CreateService(dbName, TenantA).ResolveAsync(new ResolveScanRequest("A1-B3"), default);
        byCode.Kind.Should().Be("Location");
        byCode.Location!.Id.Should().Be(LocationId);
    }

    [Fact]
    public async Task Resolve_ShouldMatchWarehouseByCode()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("WH-MAIN"), default);

        result.Kind.Should().Be("Warehouse");
        result.Warehouse!.Name.Should().Be("Main Warehouse");
    }

    [Fact]
    public async Task Resolve_ShouldMatchSalesOrderDocument()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("SO-2026-0001"), default);

        result.Kind.Should().Be("SalesOrder");
        result.Document!.PartyName.Should().Be("Acme Ltd");
        result.Document.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task Resolve_ShouldCarryStructuredQrFieldsThroughToThePayload()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(
            new ResolveScanRequest("(01)8801234567890(10)B-001(17)270131"), default);

        result.Kind.Should().Be("Product");
        result.Payload.Format.Should().Be("Gs1");
        result.Payload.BatchNumber.Should().Be("B-001");
        result.Payload.ExpiryDate.Should().Be(new DateTime(2027, 1, 31, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Resolve_ShouldReturnUnknownWithAMessage_WhenNothingMatches()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.ResolveAsync(new ResolveScanRequest("NOT-A-REAL-CODE"), default);

        result.Matched.Should().BeFalse();
        result.Kind.Should().Be("Unknown");
        result.Message.Should().Contain("NOT-A-REAL-CODE");
        result.Product.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldHonourExpectedKinds()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        // A screen asking only for a bin must not silently accept the warehouse code.
        var result = await service.ResolveAsync(
            new ResolveScanRequest("WH-MAIN", ExpectedKinds: ["Location"]), default);

        result.Matched.Should().BeFalse();
        result.Kind.Should().Be("Unknown");
    }

    [Fact]
    public async Task Resolve_ShouldNotReachAcrossTenants()
    {
        var dbName = SeedDatabase();

        // Tenant B has its own product on the same barcode and must get its own row.
        var result = await CreateService(dbName, TenantB).ResolveAsync(
            new ResolveScanRequest("8801234567890"), default);

        result.Kind.Should().Be("Product");
        result.Product!.Name.Should().Be("Other Tenant Hammer");
        result.Product.Id.Should().NotBe(ProductId);
    }

    [Fact]
    public async Task Resolve_ShouldNotFindAnotherTenantsLocation()
    {
        var dbName = SeedDatabase();

        var result = await CreateService(dbName, TenantB).ResolveAsync(
            new ResolveScanRequest("LOC-A1B3"), default);

        result.Matched.Should().BeFalse();
    }

    [Fact]
    public async Task FindProduct_ShouldResolveASerialToItsProduct()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.FindProductAsync("SN-DRILL-001", default);

        result.Kind.Should().Be("Serial");
        result.Product.Should().NotBeNull();
        result.Product!.Sku.Should().Be("TOOL-00002");
        result.Product.Serials.Should().ContainSingle(s => s.SerialNumber == "SN-DRILL-001");
    }

    [Fact]
    public async Task FindProduct_ShouldNotMatchALocationBarcode()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.FindProductAsync("LOC-A1B3", default);

        result.Matched.Should().BeFalse();
        result.Message.Should().Contain("not assigned to a product");
    }

    [Fact]
    public async Task GetAvailability_ShouldSumAcrossBatches_WhenNoBatchIsGiven()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.GetAvailabilityAsync(ProductId, WarehouseId, LocationId, null, default);

        result.QuantityOnHand.Should().Be(65);
        result.QuantityReserved.Should().Be(10);
        result.QuantityAvailable.Should().Be(55);
        // Weighted average of 40 @ 10.00 and 25 @ 12.00.
        result.UnitCost.Should().BeApproximately(10.77m, 0.01m);
    }

    [Fact]
    public async Task GetAvailability_ShouldNarrowToASingleBatch_WhenOneIsGiven()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.GetAvailabilityAsync(ProductId, WarehouseId, LocationId, "B-002", default);

        result.QuantityOnHand.Should().Be(25);
        result.QuantityAvailable.Should().Be(25);
        result.UnitCost.Should().Be(12m);
    }

    [Fact]
    public async Task GetAvailability_ShouldReportZero_ForAProductWithNoStockInThatBin()
    {
        var service = CreateService(SeedDatabase(), TenantA);

        var result = await service.GetAvailabilityAsync(SerialProductId, WarehouseId, LocationId, null, default);

        result.QuantityOnHand.Should().Be(0);
        result.QuantityAvailable.Should().Be(0);
    }
}
