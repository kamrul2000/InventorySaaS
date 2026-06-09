using FluentAssertions;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Billing;

/// <summary>Covers AR invoice generation and payment allocation across invoices.</summary>
public class BillingTests
{
    private static readonly Guid Tenant = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private sealed class FakeTenantAccessor : ITenantAccessor
    {
        public Guid? TenantId => Tenant;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public string? Email => "billing@example.com";
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

    private static Invoice NewInvoice(Guid customerId, string number, decimal total) => new()
    {
        TenantId = Tenant,
        InvoiceNumber = number,
        CustomerId = customerId,
        InvoiceDate = new DateTime(2026, 1, 1),
        DueDate = new DateTime(2026, 1, 31),
        Status = InvoiceStatus.Issued,
        SubTotal = total,
        TotalAmount = total,
        AmountPaid = 0
    };

    [Fact]
    public async Task CreateFromSalesOrder_GeneratesIssuedInvoice_WithCorrectTotals_AndBlocksDuplicate()
    {
        var dbName = Guid.NewGuid().ToString();
        var customerId = Guid.NewGuid();
        var product1 = Guid.NewGuid();
        var product2 = Guid.NewGuid();
        Guid soId;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Customers.Add(new CustomerInfo { Id = customerId, TenantId = Tenant, Name = "Acme", Code = "C-1" });
            ctx.Products.Add(new ProductInfo { Id = product1, TenantId = Tenant, Name = "Widget", Sku = "W-1", CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid() });
            ctx.Products.Add(new ProductInfo { Id = product2, TenantId = Tenant, Name = "Gadget", Sku = "G-1", CategoryId = Guid.NewGuid(), UnitOfMeasureId = Guid.NewGuid() });
            ctx.Warehouses.Add(new WarehouseInfo { TenantId = Tenant, Name = "Main", Code = "MAIN" });

            var so = new SalesOrder
            {
                TenantId = Tenant, OrderNumber = "SO-INV-0001",
                CustomerId = customerId, WarehouseId = ctx.Warehouses.Local.First().Id,
                OrderDate = DateTime.UtcNow, Status = SalesOrderStatus.Delivered
            };
            so.Items.Add(new SalesOrderItem { TenantId = Tenant, ProductId = product1, Quantity = 2, UnitPrice = 100m, TaxRate = 10m, DiscountRate = 0m, LineTotal = 220m });
            so.Items.Add(new SalesOrderItem { TenantId = Tenant, ProductId = product2, Quantity = 1, UnitPrice = 50m, TaxRate = 0m, DiscountRate = 0m, LineTotal = 50m });
            ctx.SalesOrders.Add(so);
            await ctx.SaveChangesAsync();
            soId = so.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new InvoiceService(ctx, new FakeCurrentUserService());

            var invoice = await service.CreateFromSalesOrderAsync(new CreateInvoiceFromSalesOrderRequest(soId, null), default);

            invoice.Status.Should().Be(nameof(InvoiceStatus.Issued));
            invoice.SubTotal.Should().Be(250m);
            invoice.TaxAmount.Should().Be(20m);
            invoice.TotalAmount.Should().Be(270m);
            invoice.BalanceDue.Should().Be(270m);
            invoice.SalesOrderId.Should().Be(soId);
            invoice.Items.Should().HaveCount(2);
        }

        // A second invoice for the same order must be rejected.
        using (var ctx = CreateContext(dbName))
        {
            var service = new InvoiceService(ctx, new FakeCurrentUserService());
            var act = async () => await service.CreateFromSalesOrderAsync(new CreateInvoiceFromSalesOrderRequest(soId, null), default);
            await act.Should().ThrowAsync<ConflictException>();
        }
    }

    [Fact]
    public async Task Payment_AllocatedAcrossInvoices_UpdatesBalancesAndStatuses()
    {
        var dbName = Guid.NewGuid().ToString();
        var customerId = Guid.NewGuid();
        Guid inv1Id, inv2Id;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Customers.Add(new CustomerInfo { Id = customerId, TenantId = Tenant, Name = "Acme", Code = "C-1" });
            var inv1 = NewInvoice(customerId, "INV-1", 100m);
            var inv2 = NewInvoice(customerId, "INV-2", 200m);
            ctx.Invoices.AddRange(inv1, inv2);
            await ctx.SaveChangesAsync();
            inv1Id = inv1.Id;
            inv2Id = inv2.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new PaymentService(ctx, new FakeCurrentUserService());
            var request = new CreatePaymentRequest(
                customerId, null, 250m, "BankTransfer", "TXN-REF", null,
                [
                    new CreatePaymentAllocationRequest(inv1Id, 100m),
                    new CreatePaymentAllocationRequest(inv2Id, 150m),
                ]);

            var payment = await service.CreateAsync(request, default);

            payment.Amount.Should().Be(250m);
            payment.Allocations.Should().HaveCount(2);
        }

        using (var ctx = CreateContext(dbName))
        {
            var inv1 = await ctx.Invoices.FirstAsync(i => i.Id == inv1Id);
            var inv2 = await ctx.Invoices.FirstAsync(i => i.Id == inv2Id);

            inv1.AmountPaid.Should().Be(100m);
            inv1.Status.Should().Be(InvoiceStatus.Paid);
            inv1.BalanceDue.Should().Be(0m);

            inv2.AmountPaid.Should().Be(150m);
            inv2.Status.Should().Be(InvoiceStatus.PartiallyPaid);
            inv2.BalanceDue.Should().Be(50m);
        }
    }

    [Fact]
    public async Task Payment_AllocationExceedingBalance_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        var customerId = Guid.NewGuid();
        Guid invId;

        using (var ctx = CreateContext(dbName))
        {
            ctx.Customers.Add(new CustomerInfo { Id = customerId, TenantId = Tenant, Name = "Acme", Code = "C-1" });
            var inv = NewInvoice(customerId, "INV-1", 100m);
            ctx.Invoices.Add(inv);
            await ctx.SaveChangesAsync();
            invId = inv.Id;
        }

        using (var ctx = CreateContext(dbName))
        {
            var service = new PaymentService(ctx, new FakeCurrentUserService());
            var request = new CreatePaymentRequest(
                customerId, null, 150m, "Cash", null, null,
                [new CreatePaymentAllocationRequest(invId, 150m)]);

            var act = async () => await service.CreateAsync(request, default);
            await act.Should().ThrowAsync<BadRequestException>();
        }
    }
}
