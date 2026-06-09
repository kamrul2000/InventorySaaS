using FluentAssertions;
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

namespace InventorySaaS.UnitTests.Features.SalesOrders;

/// <summary>
/// Verifies that confirming a sales order reserves stock and that cancelling it
/// releases the still-reserved (undelivered) quantity back to availability.
/// </summary>
public class SalesOrderReservationTests
{
    private static readonly Guid Tenant = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId { get; set; } = Tenant;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => "tester@example.com";
        public Guid? TenantId => Tenant;
        public bool IsSuperAdmin => false;
        public IReadOnlyList<string> Roles => [];
    }

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options, new FakeTenantAccessor(), new FakeCurrentUserService());
    }

    private static (Guid productId, Guid warehouseId, Guid customerId) Seed(ApplicationDbContext ctx)
    {
        var product = new ProductInfo
        {
            TenantId = Tenant, Name = "Widget", Sku = "W-1",
            CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid(), CostPrice = 4m
        };
        var warehouse = new WarehouseInfo { TenantId = Tenant, Name = "Main", Code = "MAIN" };
        var customer = new CustomerInfo { TenantId = Tenant, Name = "Acme", Code = "C-1" };

        ctx.Products.Add(product);
        ctx.Warehouses.Add(warehouse);
        ctx.Customers.Add(customer);
        ctx.InventoryBalances.Add(new InventoryBalance
        {
            TenantId = Tenant, ProductId = product.Id, WarehouseId = warehouse.Id,
            QuantityOnHand = 100, QuantityReserved = 0, UnitCost = 4m
        });
        ctx.SaveChanges();

        return (product.Id, warehouse.Id, customer.Id);
    }

    [Fact]
    public async Task Confirm_ThenCancel_ReleasesReservedStock()
    {
        var dbName = Guid.NewGuid().ToString();
        Guid productId, warehouseId, customerId;

        using (var seedCtx = CreateContext(dbName))
        {
            (productId, warehouseId, customerId) = Seed(seedCtx);

            var so = new SalesOrder
            {
                TenantId = Tenant, OrderNumber = "SO-TEST-0001",
                CustomerId = customerId, WarehouseId = warehouseId,
                OrderDate = DateTime.UtcNow, Status = SalesOrderStatus.Draft
            };
            so.Items.Add(new SalesOrderItem
            {
                TenantId = Tenant, ProductId = productId,
                Quantity = 30, DeliveredQuantity = 0, ReturnedQuantity = 0, UnitPrice = 10m, LineTotal = 300m
            });
            seedCtx.SalesOrders.Add(so);
            await seedCtx.SaveChangesAsync();
        }

        Guid orderId;
        // Confirm reserves stock.
        using (var ctx = CreateContext(dbName))
        {
            var service = new SalesOrderService(ctx, new FakeCurrentUserService());
            orderId = await ctx.SalesOrders.Select(s => s.Id).FirstAsync();

            var confirmed = await service.ConfirmAsync(orderId, default);
            confirmed.Status.Should().Be(nameof(SalesOrderStatus.Confirmed));

            var balance = await ctx.InventoryBalances.FirstAsync(b => b.ProductId == productId);
            balance.QuantityReserved.Should().Be(30);
            balance.QuantityAvailable.Should().Be(70);
        }

        // Cancel releases the reservation.
        using (var ctx = CreateContext(dbName))
        {
            var service = new SalesOrderService(ctx, new FakeCurrentUserService());

            var cancelled = await service.CancelAsync(orderId, default);
            cancelled.Status.Should().Be(nameof(SalesOrderStatus.Cancelled));

            var balance = await ctx.InventoryBalances.FirstAsync(b => b.ProductId == productId);
            balance.QuantityReserved.Should().Be(0);
            balance.QuantityAvailable.Should().Be(100);
        }
    }
}
