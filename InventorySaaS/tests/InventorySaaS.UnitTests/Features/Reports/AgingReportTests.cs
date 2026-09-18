using FluentAssertions;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Billing;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Reports;

/// <summary>
/// Aging is all boundary conditions — a document one day either side of a bucket edge lands in a
/// different column, and a report that quietly drops drafts or paid documents overstates the debt.
/// </summary>
public class AgingReportTests
{
    private static readonly Guid TenantId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTime AsOf = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);

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

    private static CustomerInfo AddCustomer(ApplicationDbContext db, string name)
    {
        var customer = new CustomerInfo { TenantId = TenantId, Name = name };
        db.Customers.Add(customer);
        return customer;
    }

    private static void AddInvoice(
        ApplicationDbContext db,
        CustomerInfo customer,
        int daysOverdue,
        decimal total,
        decimal paid = 0,
        InvoiceStatus status = InvoiceStatus.Issued)
    {
        db.Invoices.Add(new Invoice
        {
            TenantId = TenantId,
            InvoiceNumber = $"INV-{Guid.NewGuid().ToString()[..6]}",
            CustomerId = customer.Id,
            Customer = customer,
            InvoiceDate = AsOf.AddDays(-daysOverdue - 30),
            DueDate = AsOf.AddDays(-daysOverdue),
            Status = status,
            TotalAmount = total,
            AmountPaid = paid
        });
    }

    private static ReportService CreateService(ApplicationDbContext db) => new(db);

    [Theory]
    // daysOverdue, expected bucket
    [InlineData(-10, "current")]  // not due yet
    [InlineData(0, "current")]    // due today is not yet overdue
    [InlineData(1, "d1")]
    [InlineData(30, "d1")]        // inclusive upper edge
    [InlineData(31, "d31")]
    [InlineData(60, "d31")]
    [InlineData(61, "d61")]
    [InlineData(90, "d61")]
    [InlineData(91, "d90")]
    [InlineData(365, "d90")]
    public async Task ArAging_ShouldPlaceEachInvoiceInTheRightBucket(int daysOverdue, string expected)
    {
        using var db = CreateContext();
        var customer = AddCustomer(db, "Acme");
        AddInvoice(db, customer, daysOverdue, 100m);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetArAgingAsync(AsOf, CancellationToken.None);

        var row = result.Single();
        var actual = new Dictionary<string, decimal>
        {
            ["current"] = row.Current,
            ["d1"] = row.Days1To30,
            ["d31"] = row.Days31To60,
            ["d61"] = row.Days61To90,
            ["d90"] = row.Days90Plus,
        };

        actual[expected].Should().Be(100m);
        actual.Where(kv => kv.Key != expected).Should().OnlyContain(kv => kv.Value == 0m);
        row.Total.Should().Be(100m);
    }

    [Fact]
    public async Task ArAging_ShouldCountOnlyTheUnpaidBalance()
    {
        using var db = CreateContext();
        var customer = AddCustomer(db, "Acme");
        AddInvoice(db, customer, daysOverdue: 10, total: 500m, paid: 200m, status: InvoiceStatus.PartiallyPaid);
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetArAgingAsync(AsOf, CancellationToken.None)).Single();

        row.Days1To30.Should().Be(300m);
        row.Total.Should().Be(300m);
    }

    [Fact]
    public async Task ArAging_ShouldExcludeDraftCancelledAndSettledInvoices()
    {
        using var db = CreateContext();
        var customer = AddCustomer(db, "Acme");
        AddInvoice(db, customer, 10, 100m, status: InvoiceStatus.Draft);
        AddInvoice(db, customer, 10, 100m, status: InvoiceStatus.Cancelled);
        AddInvoice(db, customer, 10, 100m, paid: 100m, status: InvoiceStatus.Paid);
        AddInvoice(db, customer, 10, 40m);   // the only one that should count
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetArAgingAsync(AsOf, CancellationToken.None)).Single();

        row.Total.Should().Be(40m);
        row.DocumentCount.Should().Be(1);
    }

    [Fact]
    public async Task ArAging_ShouldGroupByCustomerAndReportTheOldestDebt()
    {
        using var db = CreateContext();
        var acme = AddCustomer(db, "Acme");
        var globex = AddCustomer(db, "Globex");
        AddInvoice(db, acme, daysOverdue: 5, total: 100m);
        AddInvoice(db, acme, daysOverdue: 200, total: 50m);
        AddInvoice(db, globex, daysOverdue: 1, total: 900m);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetArAgingAsync(AsOf, CancellationToken.None);

        result.Should().HaveCount(2);
        // Ordered by total outstanding, so the largest debtor leads.
        result[0].PartyName.Should().Be("Globex");

        var acmeRow = result.Single(r => r.PartyName == "Acme");
        acmeRow.DocumentCount.Should().Be(2);
        acmeRow.Total.Should().Be(150m);
        acmeRow.OldestDaysOverdue.Should().Be(200);
    }

    [Fact]
    public async Task ArAging_ShouldReportZeroAgeWhenNothingIsOverdue()
    {
        using var db = CreateContext();
        var customer = AddCustomer(db, "Acme");
        AddInvoice(db, customer, daysOverdue: -20, total: 100m);
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetArAgingAsync(AsOf, CancellationToken.None)).Single();

        // A negative age would read as "20 days overdue" on the report.
        row.OldestDaysOverdue.Should().Be(0);
        row.Current.Should().Be(100m);
    }

    [Fact]
    public async Task ApAging_ShouldBucketSupplierBillsTheSameWay()
    {
        using var db = CreateContext();
        var supplier = new SupplierInfo { TenantId = TenantId, Name = "Global Tech" };
        db.Suppliers.Add(supplier);
        db.SupplierBills.Add(new SupplierBill
        {
            TenantId = TenantId,
            BillNumber = "BILL-1",
            SupplierId = supplier.Id,
            Supplier = supplier,
            BillDate = AsOf.AddDays(-80),
            DueDate = AsOf.AddDays(-45),
            Status = BillStatus.Open,
            TotalAmount = 250m
        });
        db.SupplierBills.Add(new SupplierBill
        {
            TenantId = TenantId,
            BillNumber = "BILL-2",
            SupplierId = supplier.Id,
            Supplier = supplier,
            DueDate = AsOf.AddDays(-45),
            Status = BillStatus.Cancelled,
            TotalAmount = 999m
        });
        await db.SaveChangesAsync();

        var row = (await CreateService(db).GetApAgingAsync(AsOf, CancellationToken.None)).Single();

        row.PartyName.Should().Be("Global Tech");
        row.Days31To60.Should().Be(250m);
        row.Total.Should().Be(250m);
    }

    [Fact]
    public async Task Aging_ShouldReturnNothingWhenThereIsNoDebt()
    {
        using var db = CreateContext();
        AddCustomer(db, "Acme");
        await db.SaveChangesAsync();

        (await CreateService(db).GetArAgingAsync(AsOf, CancellationToken.None)).Should().BeEmpty();
        (await CreateService(db).GetApAgingAsync(AsOf, CancellationToken.None)).Should().BeEmpty();
    }
}
