using FluentAssertions;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Reports;

/// <summary>
/// Margin is only trustworthy if cost comes from the stock actually shipped. These pin that the
/// report reads COGS off the inventory ledger, nets returns back out, and ignores tax.
/// </summary>
public class ProfitabilityReportTests
{
    private static readonly Guid TenantId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly DateTime SaleDate = new(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

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

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(
            options,
            new FakeTenantAccessor { TenantId = TenantId },
            new FakeCurrentUserService { TenantId = TenantId });
    }

    /// <summary>Sets up one delivered order line and the ledger movement that shipped it.</summary>
    private static (ProductInfo product, SalesOrder order) Seed(
        ApplicationDbContext db,
        decimal unitPrice,
        decimal unitCost,
        int quantity,
        decimal discountRate = 0)
    {
        var category = new Category { TenantId = TenantId, Name = "Electronics" };
        var unit = new UnitOfMeasure { TenantId = TenantId, Name = "Piece", Abbreviation = "pcs" };
        var product = new ProductInfo
        {
            TenantId = TenantId,
            Name = "Wireless Mouse",
            Sku = "ELEC-001",
            CategoryId = category.Id,
            Category = category,
            UnitOfMeasureId = unit.Id,
            // Deliberately wrong: the report must not fall back to the catalogue cost.
            CostPrice = 999m
        };
        var customer = new CustomerInfo { TenantId = TenantId, Name = "Retail Alpha" };
        var order = new SalesOrder
        {
            TenantId = TenantId,
            OrderNumber = "SO-1",
            CustomerId = customer.Id,
            Customer = customer,
            OrderDate = SaleDate,
            Status = SalesOrderStatus.Delivered
        };

        db.Categories.Add(category);
        db.UnitsOfMeasure.Add(unit);
        db.Products.Add(product);
        db.Customers.Add(customer);
        db.SalesOrders.Add(order);
        db.SalesOrderItems.Add(new SalesOrderItem
        {
            TenantId = TenantId,
            SalesOrderId = order.Id,
            ProductId = product.Id,
            Quantity = quantity,
            DeliveredQuantity = quantity,
            UnitPrice = unitPrice,
            DiscountRate = discountRate,
            TaxRate = 15m
        });
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TenantId = TenantId,
            TransactionNumber = "TXN-1",
            TransactionType = TransactionType.SalesIssue,
            ProductId = product.Id,
            Product = product,
            WarehouseId = Guid.NewGuid(),
            Quantity = quantity,
            UnitCost = unitCost,
            ReferenceType = "SalesOrder",
            ReferenceId = order.Id,
            TransactionDate = SaleDate
        });

        return (product, order);
    }

    private static ReportService CreateService(ApplicationDbContext db) => new(db);

    [Fact]
    public async Task Profitability_ShouldUseLedgerCostNotCataloguePrice()
    {
        using var db = CreateContext();
        Seed(db, unitPrice: 30m, unitCost: 12m, quantity: 10);
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetProfitabilityAsync(null, null, CancellationToken.None)).Single();

        row.UnitsSold.Should().Be(10);
        row.Revenue.Should().Be(300m);
        row.Cost.Should().Be(120m);          // 10 x 12 from the ledger, not 10 x 999
        row.GrossProfit.Should().Be(180m);
        row.MarginPercent.Should().Be(60m);
    }

    [Fact]
    public async Task Profitability_ShouldApplyLineDiscountButIgnoreTax()
    {
        using var db = CreateContext();
        // 100 each less 10% = 90 net. The 15% tax rate must not inflate revenue.
        Seed(db, unitPrice: 100m, unitCost: 40m, quantity: 5, discountRate: 10m);
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetProfitabilityAsync(null, null, CancellationToken.None)).Single();

        row.Revenue.Should().Be(450m);
        row.Cost.Should().Be(200m);
        row.GrossProfit.Should().Be(250m);
    }

    [Fact]
    public async Task Profitability_ShouldNetOutReturns()
    {
        using var db = CreateContext();
        var (product, order) = Seed(db, unitPrice: 30m, unitCost: 12m, quantity: 10);

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TenantId = TenantId,
            TransactionNumber = "TXN-2",
            TransactionType = TransactionType.Return,
            ProductId = product.Id,
            Product = product,
            WarehouseId = Guid.NewGuid(),
            Quantity = 4,
            UnitCost = 12m,
            ReferenceType = "SalesOrder",
            ReferenceId = order.Id,
            TransactionDate = SaleDate.AddDays(1)
        });
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetProfitabilityAsync(null, null, CancellationToken.None)).Single();

        row.UnitsSold.Should().Be(6);        // 10 shipped, 4 came back
        row.Revenue.Should().Be(180m);
        row.Cost.Should().Be(72m);
        row.GrossProfit.Should().Be(108m);
    }

    [Fact]
    public async Task Profitability_ShouldRespectTheDateRange()
    {
        using var db = CreateContext();
        Seed(db, unitPrice: 30m, unitCost: 12m, quantity: 10);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        var inside = await service.GetProfitabilityAsync(
            SaleDate.AddDays(-1), SaleDate.AddDays(1), CancellationToken.None);
        var after = await service.GetProfitabilityAsync(
            SaleDate.AddDays(10), null, CancellationToken.None);

        inside.Should().HaveCount(1);
        after.Should().BeEmpty();
    }

    [Fact]
    public async Task Profitability_ShouldIgnoreMovementsThatAreNotSales()
    {
        using var db = CreateContext();
        var (product, _) = Seed(db, unitPrice: 30m, unitCost: 12m, quantity: 10);

        // A manual stock-out is not a sale and has no revenue behind it.
        db.InventoryTransactions.Add(new InventoryTransaction
        {
            TenantId = TenantId,
            TransactionNumber = "TXN-3",
            TransactionType = TransactionType.StockOut,
            ProductId = product.Id,
            Product = product,
            WarehouseId = Guid.NewGuid(),
            Quantity = 100,
            UnitCost = 12m,
            TransactionDate = SaleDate
        });
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetProfitabilityAsync(null, null, CancellationToken.None)).Single();

        row.UnitsSold.Should().Be(10);
        row.Cost.Should().Be(120m);
    }

    [Fact]
    public async Task Profitability_ShouldReportZeroMarginRatherThanDivideByZero()
    {
        using var db = CreateContext();
        Seed(db, unitPrice: 0m, unitCost: 5m, quantity: 3);
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetProfitabilityAsync(null, null, CancellationToken.None)).Single();

        row.Revenue.Should().Be(0m);
        row.GrossProfit.Should().Be(-15m);
        row.MarginPercent.Should().Be(0m);
    }

    [Fact]
    public async Task SalesSummary_ShouldExcludeDraftAndCancelledOrders()
    {
        using var db = CreateContext();
        var customer = new CustomerInfo { TenantId = TenantId, Name = "Retail Alpha" };
        db.Customers.Add(customer);

        foreach (var (status, total) in new[]
        {
            (SalesOrderStatus.Draft, 100m),
            (SalesOrderStatus.Cancelled, 200m),
            (SalesOrderStatus.Delivered, 300m),
            (SalesOrderStatus.Confirmed, 50m),
        })
        {
            db.SalesOrders.Add(new SalesOrder
            {
                TenantId = TenantId,
                OrderNumber = $"SO-{total}",
                CustomerId = customer.Id,
                Customer = customer,
                OrderDate = SaleDate,
                Status = status,
                SubTotal = total,
                TotalAmount = total
            });
        }
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetSalesSummaryAsync(null, null, CancellationToken.None)).Single();

        row.OrderCount.Should().Be(2);
        row.TotalAmount.Should().Be(350m);
    }
}
