using FluentAssertions;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Inventory;

/// <summary>
/// Covers the scan-driven behaviour added to stock in/out/transfer: batch preservation,
/// serial tracking, negative-stock prevention and idempotent replay.
/// </summary>
public class ScanInventoryOperationsTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid WarehouseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid OtherWarehouseId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid LocationId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid OtherLocationId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static readonly Guid PlainProductId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid BatchProductId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid SerialProductId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

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

    private static string SeedDatabase()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName, tenantId: null);

        db.Categories.Add(new Category { Id = CategoryId, TenantId = TenantId, Name = "Tools" });
        db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = UnitId, TenantId = TenantId, Name = "Piece", Abbreviation = "pc" });

        db.Warehouses.Add(new WarehouseInfo { Id = WarehouseId, TenantId = TenantId, Name = "Main", Code = "WH-1" });
        db.Warehouses.Add(new WarehouseInfo { Id = OtherWarehouseId, TenantId = TenantId, Name = "Overflow", Code = "WH-2" });

        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = LocationId, TenantId = TenantId, WarehouseId = WarehouseId, Name = "A1", Code = "A1"
        });
        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = OtherLocationId, TenantId = TenantId, WarehouseId = OtherWarehouseId, Name = "B1", Code = "B1"
        });

        db.Products.Add(NewProduct(PlainProductId, "Hammer", "TOOL-1"));
        db.Products.Add(NewProduct(BatchProductId, "Paint", "TOOL-2", trackBatch: true));
        db.Products.Add(NewProduct(SerialProductId, "Drill", "TOOL-3", trackSerial: true));

        db.SaveChangesAsync().GetAwaiter().GetResult();
        return dbName;
    }

    private static ProductInfo NewProduct(
        Guid id, string name, string sku, bool trackBatch = false, bool trackSerial = false) => new()
    {
        Id = id,
        TenantId = TenantId,
        Name = name,
        Sku = sku,
        CategoryId = CategoryId,
        UnitOfMeasureId = UnitId,
        CostPrice = 10m,
        SellingPrice = 20m,
        TrackBatch = trackBatch,
        TrackSerial = trackSerial,
    };

    private static InventoryService CreateService(ApplicationDbContext db) =>
        new(db, new FakeCurrentUserService { TenantId = TenantId });

    private static StockInRequest StockIn(
        Guid productId,
        int quantity,
        string? batch = null,
        IReadOnlyList<string>? serials = null,
        Guid? locationId = null,
        DateTime? expiry = null) =>
        new(productId, WarehouseId, locationId ?? LocationId, quantity, 10m, batch, null, expiry, null, serials);

    // -----------------------------------------------------------------------------------
    // Batch handling
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task StockIn_ShouldRejectABatchTrackedProductWithoutABatchNumber()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.StockInAsync(StockIn(BatchProductId, 5), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*batch-tracked*");
    }

    [Fact]
    public async Task StockOut_ShouldPreserveTheBatchNumberOnTheTransaction()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(BatchProductId, 10, batch: "B-1"), default);

        var result = await service.StockOutAsync(
            new StockOutRequest(BatchProductId, WarehouseId, LocationId, 4, null, Reason: "Damaged", BatchNumber: "B-1"),
            default);

        result.BatchNumber.Should().Be("B-1");
        result.Reason.Should().Be("Damaged");
    }

    [Fact]
    public async Task StockOut_ShouldNotDrawFromADifferentBatchThanTheOneNamed()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(BatchProductId, 10, batch: "B-1"), default);
        await service.StockInAsync(StockIn(BatchProductId, 10, batch: "B-2"), default);

        // 15 exists in total, but only 10 in B-1 — this must fail rather than silently
        // spilling over into B-2 and losing the batch's integrity.
        var act = () => service.StockOutAsync(
            new StockOutRequest(BatchProductId, WarehouseId, LocationId, 15, null, BatchNumber: "B-1"),
            default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Insufficient stock*");

        var remaining = await db.InventoryBalances
            .Where(b => b.ProductId == BatchProductId)
            .SumAsync(b => b.QuantityOnHand);
        remaining.Should().Be(20);
    }

    [Fact]
    public async Task StockOut_ShouldDrawEarliestExpiryFirst_ForAnUntrackedProduct()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var soonest = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var later = new DateTime(2028, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        await service.StockInAsync(StockIn(PlainProductId, 5, batch: "LATER", expiry: later), default);
        await service.StockInAsync(StockIn(PlainProductId, 5, batch: "SOON", expiry: soonest), default);

        await service.StockOutAsync(
            new StockOutRequest(PlainProductId, WarehouseId, LocationId, 5, null), default);

        var soonBalance = await db.InventoryBalances.FirstAsync(b => b.BatchNumber == "SOON");
        var laterBalance = await db.InventoryBalances.FirstAsync(b => b.BatchNumber == "LATER");

        soonBalance.QuantityOnHand.Should().Be(0);
        laterBalance.QuantityOnHand.Should().Be(5);
    }

    [Fact]
    public async Task Transfer_ShouldCarryBatchAndExpiryToTheDestination()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var expiry = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        await service.StockInAsync(StockIn(BatchProductId, 10, batch: "B-9", expiry: expiry), default);

        await service.TransferAsync(
            new StockTransferRequest(
                BatchProductId, WarehouseId, LocationId, OtherWarehouseId, OtherLocationId, 6, null, BatchNumber: "B-9"),
            default);

        var destination = await db.InventoryBalances
            .FirstAsync(b => b.WarehouseId == OtherWarehouseId && b.ProductId == BatchProductId);

        destination.QuantityOnHand.Should().Be(6);
        destination.BatchNumber.Should().Be("B-9");
        destination.ExpiryDate.Should().Be(expiry);
    }

    // -----------------------------------------------------------------------------------
    // Negative stock
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task StockOut_ShouldNeverDriveStockNegative()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(PlainProductId, 3), default);

        var act = () => service.StockOutAsync(
            new StockOutRequest(PlainProductId, WarehouseId, LocationId, 4, null), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Insufficient stock*");

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == PlainProductId);
        balance.QuantityOnHand.Should().Be(3);
    }

    [Fact]
    public async Task StockOut_ShouldNotCountReservedStockAsAvailable()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(PlainProductId, 10), default);

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == PlainProductId);
        balance.QuantityReserved = 8;
        await db.SaveChangesAsync();

        var act = () => service.StockOutAsync(
            new StockOutRequest(PlainProductId, WarehouseId, LocationId, 5, null), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Available: 2*");
    }

    // -----------------------------------------------------------------------------------
    // Serial numbers
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task StockIn_ShouldRecordEverySerialNumberIndividually()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var result = await service.StockInAsync(
            StockIn(SerialProductId, 3, serials: ["SN-1", "SN-2", "SN-3"]), default);

        result.SerialNumbers.Should().BeEquivalentTo(["SN-1", "SN-2", "SN-3"]);

        var stored = await db.ProductSerials.Where(s => s.ProductId == SerialProductId).ToListAsync();
        stored.Should().HaveCount(3);
        stored.Should().OnlyContain(s => s.Status == SerialStatus.InStock && s.WarehouseId == WarehouseId);
    }

    [Fact]
    public async Task StockIn_ShouldRejectAQuantityThatDoesNotMatchTheSerialCount()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.StockInAsync(StockIn(SerialProductId, 3, serials: ["SN-1", "SN-2"]), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*they must match*");
    }

    [Fact]
    public async Task StockIn_ShouldRejectASerialTrackedProductWithoutSerials()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.StockInAsync(StockIn(SerialProductId, 2), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*serial-tracked*");
    }

    [Fact]
    public async Task StockIn_ShouldRejectADuplicateSerialWithinTheTenant()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(SerialProductId, 1, serials: ["SN-DUP"]), default);

        var act = () => service.StockInAsync(StockIn(SerialProductId, 1, serials: ["SN-DUP"]), default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already exist*");
    }

    [Fact]
    public async Task StockIn_ShouldRejectTheSameSerialScannedTwiceInOneRequest()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.StockInAsync(StockIn(SerialProductId, 2, serials: ["SN-A", "SN-A"]), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*scanned more than once*");
    }

    [Fact]
    public async Task StockIn_ShouldRejectSerialsForAProductThatIsNotSerialTracked()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.StockInAsync(StockIn(PlainProductId, 1, serials: ["SN-X"]), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*not serial-tracked*");
    }

    [Fact]
    public async Task StockOut_ShouldMarkIssuedSerialsAndRejectReissuingThem()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(SerialProductId, 2, serials: ["SN-1", "SN-2"]), default);

        await service.StockOutAsync(
            new StockOutRequest(SerialProductId, WarehouseId, LocationId, 1, null, SerialNumbers: ["SN-1"]),
            default);

        var issued = await db.ProductSerials.FirstAsync(s => s.SerialNumber == "SN-1");
        issued.Status.Should().Be(SerialStatus.Issued);

        var act = () => service.StockOutAsync(
            new StockOutRequest(SerialProductId, WarehouseId, LocationId, 1, null, SerialNumbers: ["SN-1"]),
            default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*not in stock*");
    }

    [Fact]
    public async Task StockOut_ShouldRejectAnUnknownSerialNumber()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(SerialProductId, 1, serials: ["SN-1"]), default);

        var act = () => service.StockOutAsync(
            new StockOutRequest(SerialProductId, WarehouseId, LocationId, 1, null, SerialNumbers: ["SN-NOPE"]),
            default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Unknown serial number*");
    }

    [Fact]
    public async Task Transfer_ShouldMoveSerialsToTheDestinationRatherThanIssuingThem()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(SerialProductId, 2, serials: ["SN-1", "SN-2"]), default);

        await service.TransferAsync(
            new StockTransferRequest(
                SerialProductId, WarehouseId, LocationId, OtherWarehouseId, OtherLocationId, 1, null,
                SerialNumbers: ["SN-2"]),
            default);

        var moved = await db.ProductSerials.FirstAsync(s => s.SerialNumber == "SN-2");
        moved.Status.Should().Be(SerialStatus.InStock);
        moved.WarehouseId.Should().Be(OtherWarehouseId);
        moved.LocationId.Should().Be(OtherLocationId);
    }

    // -----------------------------------------------------------------------------------
    // Location validation and idempotency
    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task StockIn_ShouldRejectALocationFromADifferentWarehouse()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        // OtherLocationId belongs to the overflow warehouse, not the main one.
        var act = () => service.StockInAsync(StockIn(PlainProductId, 1, locationId: OtherLocationId), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*does not belong to that warehouse*");
    }

    [Fact]
    public async Task StockIn_ShouldReplayTheOriginalResult_WhenTheSameIdempotencyKeyIsReused()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var first = await service.StockInAsync(StockIn(PlainProductId, 5), default, "scan-key-1");
        var second = await service.StockInAsync(StockIn(PlainProductId, 5), default, "scan-key-1");

        // Same transaction returned, and the stock moved only once.
        second.Id.Should().Be(first.Id);
        second.TransactionNumber.Should().Be(first.TransactionNumber);

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == PlainProductId);
        balance.QuantityOnHand.Should().Be(5);

        var transactionCount = await db.InventoryTransactions.CountAsync(t => t.ProductId == PlainProductId);
        transactionCount.Should().Be(1);
    }

    [Fact]
    public async Task StockIn_ShouldTreatDifferentIdempotencyKeysAsSeparateOperations()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(PlainProductId, 5), default, "scan-key-1");
        await service.StockInAsync(StockIn(PlainProductId, 5), default, "scan-key-2");

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == PlainProductId);
        balance.QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public async Task ReusingAnIdempotencyKeyOnADifferentOperation_ShouldBeRejected()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StockInAsync(StockIn(PlainProductId, 5), default, "shared-key");

        var act = () => service.StockOutAsync(
            new StockOutRequest(PlainProductId, WarehouseId, LocationId, 1, null), default, "shared-key");

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*different operation*");
    }
}
