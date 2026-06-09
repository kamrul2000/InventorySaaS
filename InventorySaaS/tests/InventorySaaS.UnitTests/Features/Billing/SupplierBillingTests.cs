using FluentAssertions;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Purchase;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Billing;

/// <summary>Covers AP supplier-bill generation and payment allocation across bills.</summary>
public class SupplierBillingTests
{
    private static readonly Guid Tenant = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId => Tenant;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => "ap@example.com";
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

    private static SupplierBill NewBill(Guid supplierId, string number, decimal total) => new()
    {
        TenantId = Tenant,
        BillNumber = number,
        SupplierId = supplierId,
        BillDate = new DateTime(2026, 1, 1),
        DueDate = new DateTime(2026, 1, 31),
        Status = BillStatus.Open,
        SubTotal = total,
        TotalAmount = total,
        AmountPaid = 0
    };

    [Fact]
    public async Task CreateFromPurchaseOrder_GeneratesOpenBill_WithCorrectTotals_AndBlocksDuplicate()
    {
        var dbName = Guid.NewGuid().ToString();
        var supplierId = Guid.NewGuid();
        var product1 = Guid.NewGuid();
        var product2 = Guid.NewGuid();
        Guid poId;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Suppliers.Add(new SupplierInfo { Id = supplierId, TenantId = Tenant, Name = "Supplier Co", Code = "S-1" });
            ctx.Products.Add(new ProductInfo { Id = product1, TenantId = Tenant, Name = "Widget", Sku = "W-1", CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid() });
            ctx.Products.Add(new ProductInfo { Id = product2, TenantId = Tenant, Name = "Gadget", Sku = "G-1", CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid() });
            ctx.Warehouses.Add(new WarehouseInfo { TenantId = Tenant, Name = "Main", Code = "MAIN" });

            var po = new PurchaseOrder
            {
                TenantId = Tenant, OrderNumber = "PO-BILL-0001",
                SupplierId = supplierId, WarehouseId = ctx.Warehouses.Local.First().Id,
                OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatus.Received
            };
            po.Items.Add(new PurchaseOrderItem { TenantId = Tenant, ProductId = product1, Quantity = 2, ReceivedQuantity = 2, UnitPrice = 100m, TaxRate = 10m, DiscountRate = 0m, LineTotal = 220m });
            po.Items.Add(new PurchaseOrderItem { TenantId = Tenant, ProductId = product2, Quantity = 1, ReceivedQuantity = 1, UnitPrice = 50m, TaxRate = 0m, DiscountRate = 0m, LineTotal = 50m });
            ctx.PurchaseOrders.Add(po);
            await ctx.SaveChangesAsync();
            poId = po.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new SupplierBillService(ctx, new FakeCurrentUserService());

            var bill = await service.CreateFromPurchaseOrderAsync(
                new CreateBillFromPurchaseOrderRequest(poId, "SUP-INV-99", null), default);

            bill.Status.Should().Be(nameof(BillStatus.Open));
            bill.SubTotal.Should().Be(250m);
            bill.TaxAmount.Should().Be(20m);
            bill.TotalAmount.Should().Be(270m);
            bill.BalanceDue.Should().Be(270m);
            bill.PurchaseOrderId.Should().Be(poId);
            bill.SupplierInvoiceNumber.Should().Be("SUP-INV-99");
            bill.Items.Should().HaveCount(2);
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new SupplierBillService(ctx, new FakeCurrentUserService());
            var act = async () => await service.CreateFromPurchaseOrderAsync(
                new CreateBillFromPurchaseOrderRequest(poId, null, null), default);
            await act.Should().ThrowAsync<ConflictException>();
        }
    }

    [Fact]
    public async Task SupplierPayment_AllocatedAcrossBills_UpdatesBalancesAndStatuses()
    {
        var dbName = Guid.NewGuid().ToString();
        var supplierId = Guid.NewGuid();
        Guid bill1Id, bill2Id;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Suppliers.Add(new SupplierInfo { Id = supplierId, TenantId = Tenant, Name = "Supplier Co", Code = "S-1" });
            var b1 = NewBill(supplierId, "BILL-1", 100m);
            var b2 = NewBill(supplierId, "BILL-2", 200m);
            ctx.SupplierBills.AddRange(b1, b2);
            await ctx.SaveChangesAsync();
            bill1Id = b1.Id;
            bill2Id = b2.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new SupplierPaymentService(ctx, new FakeCurrentUserService());
            var request = new CreateSupplierPaymentRequest(
                supplierId, null, 250m, "BankTransfer", "TXN-REF", null,
                [
                    new CreateSupplierPaymentAllocationRequest(bill1Id, 100m),
                    new CreateSupplierPaymentAllocationRequest(bill2Id, 150m),
                ]);

            var payment = await service.CreateAsync(request, default);

            payment.Amount.Should().Be(250m);
            payment.Allocations.Should().HaveCount(2);
        }

        using (var ctx = CreateContext(dbName))
        {
            var b1 = await ctx.SupplierBills.FirstAsync(b => b.Id == bill1Id);
            var b2 = await ctx.SupplierBills.FirstAsync(b => b.Id == bill2Id);

            b1.AmountPaid.Should().Be(100m);
            b1.Status.Should().Be(BillStatus.Paid);
            b1.BalanceDue.Should().Be(0m);

            b2.AmountPaid.Should().Be(150m);
            b2.Status.Should().Be(BillStatus.PartiallyPaid);
            b2.BalanceDue.Should().Be(50m);
        }
    }

    [Fact]
    public async Task SupplierPayment_AllocationExceedingBalance_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var supplierId = Guid.NewGuid();
        Guid billId;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Suppliers.Add(new SupplierInfo { Id = supplierId, TenantId = Tenant, Name = "Supplier Co", Code = "S-1" });
            var b = NewBill(supplierId, "BILL-1", 100m);
            ctx.SupplierBills.Add(b);
            await ctx.SaveChangesAsync();
            billId = b.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new SupplierPaymentService(ctx, new FakeCurrentUserService());
            var request = new CreateSupplierPaymentRequest(
                supplierId, null, 150m, "Cash", null, null,
                [new CreateSupplierPaymentAllocationRequest(billId, 150m)]);

            var act = async () => await service.CreateAsync(request, default);
            await act.Should().ThrowAsync<BadRequestException>();
        }
    }
}
