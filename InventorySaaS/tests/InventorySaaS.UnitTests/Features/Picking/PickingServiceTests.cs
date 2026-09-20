using FluentAssertions;
using InventorySaaS.Application.Features.Picking.DTOs;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace InventorySaaS.UnitTests.Features.Picking;

public class PickingServiceTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid UnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid WarehouseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CustomerId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid OrderId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static readonly Guid HammerId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid DrillId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid OffOrderId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId { get; set; }
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => "picker@example.com";
        public Guid? TenantId { get; set; }
        public bool IsSuperAdmin => false;
        public IReadOnlyList<string> Roles => [];
    }

    private static ApplicationDbContext CreateContext(string dbName, Guid? tenantId) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(dbName).Options,
            new FakeTenantAccessor { TenantId = tenantId },
            new FakeCurrentUserService { TenantId = tenantId });

    private static string SeedDatabase(SalesOrderStatus status = SalesOrderStatus.Confirmed)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateContext(dbName, tenantId: null);

        db.Categories.Add(new Category { Id = CategoryId, TenantId = TenantId, Name = "Tools" });
        db.UnitsOfMeasure.Add(new UnitOfMeasure { Id = UnitId, TenantId = TenantId, Name = "Piece", Abbreviation = "pc" });
        db.Warehouses.Add(new WarehouseInfo { Id = WarehouseId, TenantId = TenantId, Name = "Main", Code = "WH-1" });
        db.Customers.Add(new CustomerInfo { Id = CustomerId, TenantId = TenantId, Name = "Acme Ltd" });

        db.Products.Add(NewProduct(HammerId, "Hammer", "TOOL-1", "BC-HAMMER"));
        db.Products.Add(NewProduct(DrillId, "Drill", "TOOL-2", "BC-DRILL"));
        db.Products.Add(NewProduct(OffOrderId, "Ladder", "TOOL-3", "BC-LADDER"));

        db.SalesOrders.Add(new SalesOrder
        {
            Id = OrderId,
            TenantId = TenantId,
            OrderNumber = "SO-2026-0001",
            CustomerId = CustomerId,
            WarehouseId = WarehouseId,
            Status = status,
            Items =
            [
                new SalesOrderItem { TenantId = TenantId, ProductId = HammerId, Quantity = 5, UnitPrice = 10m },
                new SalesOrderItem { TenantId = TenantId, ProductId = DrillId, Quantity = 2, UnitPrice = 80m },
            ],
        });

        db.SaveChangesAsync().GetAwaiter().GetResult();
        return dbName;
    }

    private static ProductInfo NewProduct(Guid id, string name, string sku, string barcode) => new()
    {
        Id = id,
        TenantId = TenantId,
        Name = name,
        Sku = sku,
        Barcode = barcode,
        CategoryId = CategoryId,
        UnitOfMeasureId = UnitId,
    };

    private static PickingService CreateService(
        ApplicationDbContext db,
        Mock<ISalesOrderService>? salesOrders = null) =>
        new(db, new FakeCurrentUserService { TenantId = TenantId }, (salesOrders ?? new Mock<ISalesOrderService>()).Object);

    private static async Task<PickingService> StartedAsync(ApplicationDbContext db)
    {
        var service = CreateService(db);
        await service.StartAsync(OrderId, new StartPickRequest(null), default);
        return service;
    }

    // -----------------------------------------------------------------------------------

    [Fact]
    public async Task Start_ShouldListEveryOrderLineWithItsRequestedQuantity()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var session = await service.StartAsync(OrderId, new StartPickRequest("Bay 3"), default);

        session.Status.Should().Be("Open");
        session.OrderNumber.Should().Be("SO-2026-0001");
        session.CustomerName.Should().Be("Acme Ltd");
        session.TotalRequested.Should().Be(7);
        session.TotalPicked.Should().Be(0);
        session.TotalRemaining.Should().Be(7);
        session.IsFullyPicked.Should().BeFalse();
        session.Lines.Should().HaveCount(2);
        session.Lines.Should().Contain(l => l.ProductSku == "TOOL-1" && l.RequestedQuantity == 5);
    }

    [Fact]
    public async Task Start_ShouldRejectAnOrderThatIsNotConfirmed()
    {
        using var db = CreateContext(SeedDatabase(SalesOrderStatus.Draft), TenantId);
        var service = CreateService(db);

        var act = () => service.StartAsync(OrderId, new StartPickRequest(null), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Confirm the order first*");
    }

    [Fact]
    public async Task Start_ShouldReturnTheExistingSession_RatherThanOpeningASecond()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var first = await service.StartAsync(OrderId, new StartPickRequest(null), default);
        var second = await service.StartAsync(OrderId, new StartPickRequest(null), default);

        second.Id.Should().Be(first.Id);
        (await db.SalesOrderPickSessions.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Scan_ShouldTrackRequestedPickedAndRemaining()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        var result = await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER", 2), default);

        result.Accepted.Should().BeTrue();
        result.Line!.PickedQuantity.Should().Be(2);
        result.Line.RemainingQuantity.Should().Be(3);
        result.Session.TotalPicked.Should().Be(2);
        result.Session.TotalRemaining.Should().Be(5);
    }

    [Fact]
    public async Task Scan_ShouldAccumulateAcrossRepeatedScans()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER"), default);
        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER"), default);
        var third = await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER"), default);

        third.Line!.PickedQuantity.Should().Be(3);

        // Each scan is kept as its own event, so the picking trail survives.
        (await db.SalesOrderPickEvents.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task Scan_ShouldRejectAProductThatIsNotOnTheOrder()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        var act = () => service.ScanAsync(OrderId, new PickScanRequest(null, "BC-LADDER"), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*not on this order*");
    }

    [Fact]
    public async Task Scan_ShouldPreventOverPicking()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        var act = () => service.ScanAsync(OrderId, new PickScanRequest(null, "BC-DRILL", 3), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Only 2 more*");

        var item = await db.SalesOrderItems.FirstAsync(i => i.ProductId == DrillId);
        item.PickedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Scan_ShouldRejectAnotherScanOnceALineIsComplete()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-DRILL", 2), default);

        var act = () => service.ScanAsync(OrderId, new PickScanRequest(null, "BC-DRILL"), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*already fully picked*");
    }

    [Fact]
    public async Task Scan_ShouldResolveAProductBySkuAsWellAsBarcode()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        var result = await service.ScanAsync(OrderId, new PickScanRequest(null, "TOOL-1"), default);

        result.Line!.ProductSku.Should().Be("TOOL-1");
    }

    [Fact]
    public async Task Scan_ShouldRejectAnUnknownBarcode()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        var act = () => service.ScanAsync(OrderId, new PickScanRequest(null, "NOT-A-CODE"), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*not assigned to a product*");
    }

    [Fact]
    public async Task Scan_ShouldRequireAnOpenSession()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        var act = () => service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER"), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Start picking first*");
    }

    [Fact]
    public async Task Scan_ShouldNotCountADoubleSubmissionTwice_WhenTheIdempotencyKeyRepeats()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER"), default, "pick-1");
        var replay = await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER"), default, "pick-1");

        replay.Line!.PickedQuantity.Should().Be(1);

        var item = await db.SalesOrderItems.FirstAsync(i => i.ProductId == HammerId);
        item.PickedQuantity.Should().Be(1);
    }

    [Fact]
    public async Task Complete_ShouldRefuseWhileAnyLineIsOutstanding()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER", 5), default);

        var act = () => service.CompleteAsync(OrderId, new CompletePickRequest(), default);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Still outstanding: Drill (0/2)*");
    }

    [Fact]
    public async Task Complete_ShouldCloseTheSessionOnceEveryLineIsPicked()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER", 5), default);
        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-DRILL", 2), default);

        var session = await service.CompleteAsync(OrderId, new CompletePickRequest(), default);

        session.Status.Should().Be("Completed");
        session.CompletedAt.Should().NotBeNull();
        session.IsFullyPicked.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_WithDeliver_ShouldHandPickedQuantitiesToTheExistingDeliveryPath()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var salesOrders = new Mock<ISalesOrderService>();
        var service = CreateService(db, salesOrders);

        await service.StartAsync(OrderId, new StartPickRequest(null), default);
        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER", 5), default);
        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-DRILL", 2), default);

        await service.CompleteAsync(OrderId, new CompletePickRequest(Deliver: true), default);

        // Picking must not reimplement the draw-down; it delegates to the order service.
        salesOrders.Verify(
            s => s.DeliverAsync(
                OrderId,
                It.Is<DeliverSalesOrderRequest>(r =>
                    r.Items.Count == 2 &&
                    r.Items.Any(i => i.ProductId == HammerId && i.Quantity == 5) &&
                    r.Items.Any(i => i.ProductId == DrillId && i.Quantity == 2)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Cancel_ShouldClearThePickedQuantitiesItRecorded()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = await StartedAsync(db);

        await service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER", 3), default);

        var session = await service.CancelAsync(OrderId, default);

        session.Status.Should().Be("Cancelled");
        session.TotalPicked.Should().Be(0);

        var item = await db.SalesOrderItems.FirstAsync(i => i.ProductId == HammerId);
        item.PickedQuantity.Should().Be(0);
    }

    [Fact]
    public async Task Get_ShouldReturnNull_WhenNoSessionIsOpen()
    {
        using var db = CreateContext(SeedDatabase(), TenantId);
        var service = CreateService(db);

        (await service.GetAsync(OrderId, default)).Should().BeNull();
    }

    [Fact]
    public async Task Picking_ShouldOnlyCoverTheUndeliveredRemainder()
    {
        var dbName = SeedDatabase(SalesOrderStatus.PartiallyDelivered);

        using (var seed = CreateContext(dbName, tenantId: null))
        {
            // Three hammers already went out on an earlier delivery.
            var item = await seed.SalesOrderItems.FirstAsync(i => i.ProductId == HammerId);
            item.DeliveredQuantity = 3;
            await seed.SaveChangesAsync();
        }

        using var db = CreateContext(dbName, TenantId);
        var service = await StartedAsync(db);

        var session = await service.GetAsync(OrderId, default);
        var hammerLine = session!.Lines.First(l => l.ProductId == HammerId);

        hammerLine.RequestedQuantity.Should().Be(2);
        hammerLine.DeliveredQuantity.Should().Be(3);

        var act = () => service.ScanAsync(OrderId, new PickScanRequest(null, "BC-HAMMER", 3), default);
        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*Only 2 more*");
    }
}
