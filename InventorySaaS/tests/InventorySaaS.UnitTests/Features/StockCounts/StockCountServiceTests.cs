using FluentAssertions;
using InventorySaaS.Application.Features.StockCounts.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.StockCounts;

/// <summary>
/// Counting must never change stock on its own; only Manager approval turns variances into
/// adjustments. These tests pin that boundary as much as the arithmetic.
/// </summary>
public class StockCountServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid WarehouseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid LocationId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid ForeignLocationId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid HammerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid PaintId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId { get; set; }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => "manager@example.com";
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

        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = LocationId, TenantId = TenantId, WarehouseId = WarehouseId, Name = "A1", Code = "A1",
        });

        // Belongs to a different warehouse, for the cross-warehouse guard.
        var otherWarehouseId = Guid.NewGuid();
        db.Warehouses.Add(new WarehouseInfo { Id = otherWarehouseId, TenantId = TenantId, Name = "Other", Code = "WH-2" });
        db.WarehouseLocations.Add(new WarehouseLocation
        {
            Id = ForeignLocationId, TenantId = TenantId, WarehouseId = otherWarehouseId, Name = "Z9", Code = "Z9",
        });

        db.Products.Add(new ProductInfo
        {
            Id = HammerId, TenantId = TenantId, Name = "Hammer", Sku = "TOOL-1", Barcode = "BC-HAMMER",
            CategoryId = CategoryId, UnitOfMeasureId = UnitId, CostPrice = 10m,
        });
        db.Products.Add(new ProductInfo
        {
            Id = PaintId, TenantId = TenantId, Name = "Paint", Sku = "TOOL-2", Barcode = "BC-PAINT",
            CategoryId = CategoryId, UnitOfMeasureId = UnitId, CostPrice = 25m, TrackBatch = true,
        });

        // System believes there are 10 hammers in bin A1.
        db.InventoryBalances.Add(new InventoryBalance
        {
            TenantId = TenantId, ProductId = HammerId, WarehouseId = WarehouseId, LocationId = LocationId,
            QuantityOnHand = 10, UnitCost = 10m,
        });

        db.SaveChangesAsync().GetAwaiter().GetResult();
        return dbName;
    }

    private static StockCountService CreateService(ApplicationDbContext db) =>
        new(db, new FakeCurrentUserService { TenantId = TenantId });

    private static async Task<(StockCountService Service, Guid SessionId)> StartedAsync(
        ApplicationDbContext db,
        Guid? locationId = null)
    {
        var service = CreateService(db);
        var session = await service.StartAsync(
            new StartStockCountRequest(WarehouseId, locationId ?? LocationId, null), default);

        return (service, session.Id);
    }

    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldOpenACountWithItsOwnNumber()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var session = await service.StartAsync(new StartStockCountRequest(WarehouseId, LocationId, "Q3 count"), default);

        session.Status.Should().Be("Counting");
        session.CountNumber.Should().StartWith("CNT-");
        session.WarehouseName.Should().Be("Main");
        session.LineCount.Should().Be(0);
    }

    [Fact]
    public async Task Start_ShouldRefuseASecondCountOverTheSameScope()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        await service.StartAsync(new StartStockCountRequest(WarehouseId, LocationId, null), default);

        var act = () => service.StartAsync(new StartStockCountRequest(WarehouseId, LocationId, null), default);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*already in progress*");
    }

    [Fact]
    public async Task Start_ShouldRejectALocationFromAnotherWarehouse()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.StartAsync(new StartStockCountRequest(WarehouseId, ForeignLocationId, null), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*does not belong to that warehouse*");
    }

    [Fact]
    public async Task Scan_ShouldSnapshotTheSystemQuantityAndReportTheVariance()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        var result = await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 8, SetExactQuantity: true), default);

        result.Line!.SystemQuantity.Should().Be(10);
        result.Line.CountedQuantity.Should().Be(8);
        result.Line.Variance.Should().Be(-2);
        result.Message.Should().Contain("2 short");
    }

    [Fact]
    public async Task Scan_ShouldAccumulateRepeatScansOntoOneLine()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER"), default);
        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER"), default);
        var third = await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER"), default);

        third.Session.LineCount.Should().Be(1);
        third.Line!.CountedQuantity.Should().Be(3);
    }

    [Fact]
    public async Task Scan_ShouldKeepBatchesOnSeparateLines()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-PAINT", 4, BatchNumber: "B-1"), default);
        var second = await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-PAINT", 6, BatchNumber: "B-2"), default);

        second.Session.LineCount.Should().Be(2);
        second.Session.Lines.Should().Contain(l => l.BatchNumber == "B-1" && l.CountedQuantity == 4);
        second.Session.Lines.Should().Contain(l => l.BatchNumber == "B-2" && l.CountedQuantity == 6);
    }

    [Fact]
    public async Task Scan_ShouldCountSerialsIndividuallyAndRejectARepeat()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", SerialNumber: "SN-1"), default);
        var second = await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", SerialNumber: "SN-2"), default);

        second.Line!.CountedQuantity.Should().Be(2);
        second.Line.Serials.Should().BeEquivalentTo(["SN-1", "SN-2"]);

        var act = () => service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", SerialNumber: "SN-1"), default);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*already been counted*");
    }

    [Fact]
    public async Task Scan_ShouldNotChangeInventory()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 3, SetExactQuantity: true), default);

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == HammerId);
        balance.QuantityOnHand.Should().Be(10);
        (await db.InventoryTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Scan_ShouldRejectAnUnknownBarcode()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        var act = () => service.ScanAsync(sessionId, new StockCountScanRequest(null, "NOPE"), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*not assigned to a product*");
    }

    [Fact]
    public async Task Scan_ShouldNotCountADoubleSubmissionTwice_WhenTheIdempotencyKeyRepeats()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER"), default, "count-1");
        var replay = await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER"), default, "count-1");

        replay.Line!.CountedQuantity.Should().Be(1);
    }

    [Fact]
    public async Task Submit_ShouldStopFurtherCounting()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 8, SetExactQuantity: true), default);
        var submitted = await service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);

        submitted.Status.Should().Be("PendingApproval");
        submitted.SubmittedAt.Should().NotBeNull();

        var act = () => service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER"), default);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*can no longer be changed*");
    }

    [Fact]
    public async Task Submit_ShouldRefuseAnEmptyCount()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        var act = () => service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Nothing has been counted*");
    }

    [Fact]
    public async Task Approve_ShouldRequireSubmissionFirst()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 8, SetExactQuantity: true), default);

        var act = () => service.ApproveAsync(sessionId, new ApproveStockCountRequest(null), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*awaiting approval*");
    }

    [Fact]
    public async Task Approve_ShouldPostAnAdjustmentBringingStockToTheCountedQuantity()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 8, SetExactQuantity: true), default);
        await service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);

        var approved = await service.ApproveAsync(sessionId, new ApproveStockCountRequest("Verified"), default);

        approved.Status.Should().Be("Approved");
        approved.ApprovedBy.Should().Be("manager@example.com");
        approved.ShortageLines.Should().Be(1);
        approved.NetVariance.Should().Be(-2);

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == HammerId);
        balance.QuantityOnHand.Should().Be(8);

        var transaction = await db.InventoryTransactions.SingleAsync();
        transaction.TransactionType.Should().Be(TransactionType.Adjustment);
        transaction.Quantity.Should().Be(-2);
        transaction.Reason.Should().Be("Stock count");
        transaction.ReferenceType.Should().Be("StockCount");
        transaction.ReferenceId.Should().Be(sessionId);
    }

    [Fact]
    public async Task Approve_ShouldCreateABalanceForAProductFoundThatTheSystemDidNotKnowAbout()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        // Paint has no balance row at all; counting 5 is a pure overage.
        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-PAINT", 5, BatchNumber: "B-1", SetExactQuantity: true), default);
        await service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);
        var approved = await service.ApproveAsync(sessionId, new ApproveStockCountRequest(null), default);

        approved.OverageLines.Should().Be(1);

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == PaintId);
        balance.QuantityOnHand.Should().Be(5);
        balance.BatchNumber.Should().Be("B-1");
        balance.UnitCost.Should().Be(25m);
    }

    [Fact]
    public async Task Approve_ShouldWriteNothingForALineThatMatchesTheSystem()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 10, SetExactQuantity: true), default);
        await service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);
        await service.ApproveAsync(sessionId, new ApproveStockCountRequest(null), default);

        (await db.InventoryTransactions.CountAsync()).Should().Be(0);

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == HammerId);
        balance.QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public async Task Approve_ShouldLandOnTheCountedFigure_EvenIfStockMovedSinceSubmission()
    {
        var dbName = SeedDatabase();
        using var db = CreateContext(dbName, TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 8, SetExactQuantity: true), default);
        await service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);

        // Someone issues 3 between submission and approval.
        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == HammerId);
        balance.QuantityOnHand = 7;
        await db.SaveChangesAsync();

        await service.ApproveAsync(sessionId, new ApproveStockCountRequest(null), default);

        var updated = await db.InventoryBalances.FirstAsync(b => b.ProductId == HammerId);
        updated.QuantityOnHand.Should().Be(8);

        // The adjustment is measured against the live quantity, not the stale snapshot.
        var transaction = await db.InventoryTransactions.SingleAsync();
        transaction.Quantity.Should().Be(1);
    }

    [Fact]
    public async Task Cancel_ShouldRefuseOnceApproved()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 8, SetExactQuantity: true), default);
        await service.SubmitAsync(sessionId, new SubmitStockCountRequest(null), default);
        await service.ApproveAsync(sessionId, new ApproveStockCountRequest(null), default);

        var act = () => service.CancelAsync(sessionId, default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*already posted*");
    }

    [Fact]
    public async Task Cancel_ShouldLeaveInventoryUntouched()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var (service, sessionId) = await StartedAsync(db);

        await service.ScanAsync(sessionId, new StockCountScanRequest(null, "BC-HAMMER", 2, SetExactQuantity: true), default);
        var cancelled = await service.CancelAsync(sessionId, default);

        cancelled.Status.Should().Be("Cancelled");

        var balance = await db.InventoryBalances.FirstAsync(b => b.ProductId == HammerId);
        balance.QuantityOnHand.Should().Be(10);
        (await db.InventoryTransactions.CountAsync()).Should().Be(0);
    }
}
