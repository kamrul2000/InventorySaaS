using FluentAssertions;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Purchase;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.PurchaseOrders;

/// <summary>
/// Verifies returning received goods to a supplier reduces on-hand stock, tracks the
/// returned quantity, and flips the order to Returned once everything received is sent back.
/// </summary>
public class PurchaseReturnTests
{
    private static readonly Guid Tenant = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId => Tenant;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => "buyer@example.com";
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

    [Fact]
    public async Task Return_DeductsStock_TracksQuantity_AndCompletesWhenFullyReturned()
    {
        var dbName = Guid.NewGuid().ToString();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        Guid poId;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Products.Add(new ProductInfo
            {
                Id = productId, TenantId = Tenant, Name = "Bolt", Sku = "B-1",
                CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid(), CostPrice = 3m
            });
            ctx.Warehouses.Add(new WarehouseInfo { Id = warehouseId, TenantId = Tenant, Name = "Main", Code = "MAIN" });
            ctx.Suppliers.Add(new SupplierInfo { TenantId = Tenant, Name = "Supplier Co", Code = "S-1" });
            ctx.InventoryBalances.Add(new InventoryBalance
            {
                TenantId = Tenant, ProductId = productId, WarehouseId = warehouseId,
                QuantityOnHand = 50, QuantityReserved = 0, UnitCost = 3m
            });

            var po = new PurchaseOrder
            {
                TenantId = Tenant, OrderNumber = "PO-TEST-0001",
                SupplierId = ctx.Suppliers.Local.First().Id, WarehouseId = warehouseId,
                OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatus.Received
            };
            po.Items.Add(new PurchaseOrderItem
            {
                TenantId = Tenant, ProductId = productId,
                Quantity = 50, ReceivedQuantity = 50, ReturnedQuantity = 0, UnitPrice = 3m, LineTotal = 150m
            });
            ctx.PurchaseOrders.Add(po);
            await ctx.SaveChangesAsync();
            poId = po.Id;
        }

        // Partial return of 20 → still Received.
        using (var ctx = CreateContext(dbName))
        {
            var service = new PurchaseOrderService(ctx, new FakeCurrentUserService());
            var request = new ReturnPurchaseOrderRequest(poId,
                [new ReturnPurchaseOrderItemRequest(productId, 20, "Damaged")], "Damaged batch");

            var result = await service.ReturnAsync(poId, request, default);

            result.Status.Should().Be(nameof(PurchaseOrderStatus.Received));
            result.Items[0].ReturnedQuantity.Should().Be(20);

            var balance = await ctx.InventoryBalances.FirstAsync(b => b.ProductId == productId);
            balance.QuantityOnHand.Should().Be(30);
        }

        // Return remaining 30 → Returned.
        using (var ctx = CreateContext(dbName))
        {
            var service = new PurchaseOrderService(ctx, new FakeCurrentUserService());
            var request = new ReturnPurchaseOrderRequest(poId,
                [new ReturnPurchaseOrderItemRequest(productId, 30, null)], null);

            var result = await service.ReturnAsync(poId, request, default);

            result.Status.Should().Be(nameof(PurchaseOrderStatus.Returned));
            result.Items[0].ReturnedQuantity.Should().Be(50);

            var balance = await ctx.InventoryBalances.FirstAsync(b => b.ProductId == productId);
            balance.QuantityOnHand.Should().Be(0);
        }
    }

    [Fact]
    public async Task Return_ShouldReject_WhenExceedingReceivedQuantity()
    {
        var dbName = Guid.NewGuid().ToString();
        var productId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        Guid poId;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Products.Add(new ProductInfo
            {
                Id = productId, TenantId = Tenant, Name = "Nut", Sku = "N-1",
                CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid(), CostPrice = 2m
            });
            ctx.Warehouses.Add(new WarehouseInfo { Id = warehouseId, TenantId = Tenant, Name = "Main", Code = "MAIN" });
            ctx.Suppliers.Add(new SupplierInfo { TenantId = Tenant, Name = "Supplier Co", Code = "S-1" });
            ctx.InventoryBalances.Add(new InventoryBalance
            {
                TenantId = Tenant, ProductId = productId, WarehouseId = warehouseId,
                QuantityOnHand = 10, QuantityReserved = 0, UnitCost = 2m
            });

            var po = new PurchaseOrder
            {
                TenantId = Tenant, OrderNumber = "PO-TEST-0002",
                SupplierId = ctx.Suppliers.Local.First().Id, WarehouseId = warehouseId,
                OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatus.Received
            };
            po.Items.Add(new PurchaseOrderItem
            {
                TenantId = Tenant, ProductId = productId,
                Quantity = 10, ReceivedQuantity = 10, ReturnedQuantity = 0, UnitPrice = 2m, LineTotal = 20m
            });
            ctx.PurchaseOrders.Add(po);
            await ctx.SaveChangesAsync();
            poId = po.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new PurchaseOrderService(ctx, new FakeCurrentUserService());
            var request = new ReturnPurchaseOrderRequest(poId,
                [new ReturnPurchaseOrderItemRequest(productId, 11, null)], null);

            var act = async () => await service.ReturnAsync(poId, request, default);

            await act.Should().ThrowAsync<Domain.Exceptions.BadRequestException>();
        }
    }
}
