using FluentAssertions;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Exceptions;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.UnitTests.Features.Products;

/// <summary>
/// Covers the import contract: preview never writes, invalid rows are skipped rather than
/// half-applied, unknown master records are refused unless explicitly opted into, and SKUs
/// stay unique across a batch that is saved in one go.
/// </summary>
public class ProductImportTests
{
    private static readonly Guid TenantId = Guid.Parse("33333333-3333-3333-3333-333333333333");

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

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(
            options,
            new FakeTenantAccessor { TenantId = TenantId },
            new FakeCurrentUserService { TenantId = TenantId });
    }

    /// <summary>Seeds the masters the happy-path CSV refers to.</summary>
    private static async Task<ApplicationDbContext> CreateSeededContextAsync(string dbName)
    {
        var context = CreateContext(dbName);

        context.Categories.Add(new Category { TenantId = TenantId, Name = "Electronics" });
        context.Brands.Add(new Brand { TenantId = TenantId, Name = "TechBrand" });
        context.UnitsOfMeasure.Add(new UnitOfMeasure { TenantId = TenantId, Name = "Piece", Abbreviation = "pcs" });
        await context.SaveChangesAsync();

        return context;
    }

    private static ProductImportService CreateService(ApplicationDbContext context) =>
        new(context, new FakeCurrentUserService { TenantId = TenantId });

    private const string Header = "Name,Sku,Barcode,Category,Brand,Unit,CostPrice,SellingPrice,ReorderLevel,TrackExpiry,Description";

    [Fact]
    public async Task Preview_ShouldValidateWithoutWritingAnything()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\nMouse,,123,Electronics,TechBrand,Piece,15.00,29.99,50,false,\r\n";
        var result = await service.PreviewAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.IsPreview.Should().BeTrue();
        result.TotalRows.Should().Be(1);
        result.ValidRows.Should().Be(1);
        result.ImportedRows.Should().Be(0);

        (await context.Products.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Import_ShouldCreateProductsAndGenerateSkus()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\n" +
                  "Mouse,,,Electronics,TechBrand,Piece,15.00,29.99,50,false,\r\n" +
                  "Keyboard,,,Electronics,TechBrand,Piece,45.00,89.99,20,false,\r\n";

        var result = await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.ImportedRows.Should().Be(2);
        result.InvalidRows.Should().Be(0);

        var products = await context.Products.OrderBy(p => p.Name).ToListAsync();
        products.Should().HaveCount(2);

        // Generated SKUs must differ even though nothing was saved between the two rows.
        products.Select(p => p.Sku).Should().OnlyHaveUniqueItems();
        products.Should().OnlyContain(p => p.Sku.StartsWith("ELE-"));
    }

    [Fact]
    public async Task Import_ShouldHonourASuppliedSku()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\nMouse,MY-SKU-1,,Electronics,,Piece,15.00,29.99,,,\r\n";
        await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        (await context.Products.SingleAsync()).Sku.Should().Be("MY-SKU-1");
    }

    [Fact]
    public async Task Import_ShouldRejectADuplicateSkuWithinTheSameFile()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\n" +
                  "Mouse,DUP-1,,Electronics,,Piece,15.00,29.99,,,\r\n" +
                  "Keyboard,DUP-1,,Electronics,,Piece,45.00,89.99,,,\r\n";

        var result = await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.ImportedRows.Should().Be(1);
        result.InvalidRows.Should().Be(1);
        result.Rows.Last().Errors.Should().ContainMatch("*already exists*");
    }

    [Fact]
    public async Task Import_ShouldRejectASkuAlreadyTakenBySoftDeletedProduct()
    {
        var dbName = Guid.NewGuid().ToString();
        using var context = await CreateSeededContextAsync(dbName);

        // The unique index on (TenantId, Sku) is not filtered, so a soft-deleted row still holds its SKU.
        context.Products.Add(new ProductInfo
        {
            TenantId = TenantId,
            Name = "Retired",
            Sku = "GONE-1",
            CategoryId = (await context.Categories.FirstAsync()).Id,
            UnitOfMeasureId = (await context.UnitsOfMeasure.FirstAsync()).Id,
            IsDeleted = true
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var csv = $"{Header}\r\nMouse,GONE-1,,Electronics,,Piece,15.00,29.99,,,\r\n";

        var result = await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.InvalidRows.Should().Be(1);
        result.ImportedRows.Should().Be(0);
    }

    [Fact]
    public async Task Import_ShouldRefuseUnknownMastersByDefault()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\nWidget,,,Hardware,NoSuchBrand,Piece,1.00,2.00,,,\r\n";
        var result = await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.InvalidRows.Should().Be(1);
        result.Rows[0].Errors.Should().ContainMatch("*Category 'Hardware' does not exist*");
        (await context.Categories.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Import_ShouldCreateMissingMastersWhenOptedIn()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\nWidget,,,Hardware,NewBrand,Dozen,1.00,2.00,,,\r\n";
        var result = await service.ImportAsync(csv, createMissingMasters: true, CancellationToken.None);

        result.ImportedRows.Should().Be(1);
        result.NewCategories.Should().Contain("Hardware");
        result.NewBrands.Should().Contain("NewBrand");
        result.NewUnits.Should().Contain("Dozen");

        (await context.Categories.AnyAsync(c => c.Name == "Hardware")).Should().BeTrue();
        (await context.Products.SingleAsync()).Sku.Should().StartWith("HAR-");
    }

    [Fact]
    public async Task Import_ShouldGiveNewUnitsDistinctAbbreviations()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        // "Pieces" and "Piecemeal" both derive "pie", which the unit master treats as a duplicate.
        var csv = $"{Header}\r\n" +
                  "A,,,Electronics,,Pieces,1.00,2.00,,,\r\n" +
                  "B,,,Electronics,,Piecemeal,1.00,2.00,,,\r\n";

        await service.ImportAsync(csv, createMissingMasters: true, CancellationToken.None);

        var abbreviations = await context.UnitsOfMeasure.Select(u => u.Abbreviation).ToListAsync();
        abbreviations.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Import_ShouldReportBadNumbersPerRowAndStillImportTheRest()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\n" +
                  "Good,,,Electronics,,Piece,15.00,29.99,,,\r\n" +
                  "BadPrice,,,Electronics,,Piece,abc,29.99,,,\r\n" +
                  "Negative,,,Electronics,,Piece,-5,29.99,,,\r\n";

        var result = await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.ImportedRows.Should().Be(1);
        result.InvalidRows.Should().Be(2);
        result.Rows[1].Errors.Should().ContainMatch("*not a valid number*");
        result.Rows[2].Errors.Should().ContainMatch("*cannot be negative*");

        (await context.Products.SingleAsync()).Name.Should().Be("Good");
    }

    [Fact]
    public async Task Import_ShouldReportTheSpreadsheetLineNumber()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\nBad,,,Nope,,Piece,1,2,,,\r\n";
        var result = await service.PreviewAsync(csv, createMissingMasters: false, CancellationToken.None);

        // Header is line 1, so the first data row is line 2.
        result.Rows[0].LineNumber.Should().Be(2);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("1")]
    public async Task Import_ShouldAcceptCommonBooleanSpellings(string value)
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = $"{Header}\r\nMilk,,,Electronics,,Piece,1.00,2.00,,{value},\r\n";
        await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        (await context.Products.SingleAsync()).TrackExpiry.Should().BeTrue();
    }

    [Fact]
    public async Task Import_ShouldAcceptColumnsInAnyOrder()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var csv = "SellingPrice,Name,Unit,CostPrice,Category\r\n29.99,Mouse,Piece,15.00,Electronics\r\n";
        var result = await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        result.ImportedRows.Should().Be(1);
        (await context.Products.SingleAsync()).SellingPrice.Should().Be(29.99m);
    }

    [Fact]
    public async Task Import_ShouldRejectAFileMissingRequiredColumns()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var act = () => service.ImportAsync("Name,Sku\r\nMouse,X\r\n", false, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Category, Unit, CostPrice, SellingPrice*");
    }

    [Fact]
    public async Task Import_ShouldRejectAFileWithNoDataRows()
    {
        using var context = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var service = CreateService(context);

        var act = () => service.ImportAsync($"{Header}\r\n", false, CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>().WithMessage("*no data rows*");
    }

    [Fact]
    public async Task Export_ShouldProduceAFileTheImporterAccepts()
    {
        var dbName = Guid.NewGuid().ToString();
        using var source = await CreateSeededContextAsync(dbName);
        var service = CreateService(source);

        var csv = $"{Header}\r\nMouse, ,\"1,23\",Electronics,TechBrand,Piece,15.00,29.99,50,true,\"Quoted, description\"\r\n";
        await service.ImportAsync(csv, createMissingMasters: false, CancellationToken.None);

        var exported = System.Text.Encoding.UTF8.GetString(await service.ExportAsync(CancellationToken.None));

        // Re-import into a fresh tenant database: the round trip must survive quoting.
        using var target = await CreateSeededContextAsync(Guid.NewGuid().ToString());
        var targetService = CreateService(target);
        var result = await targetService.ImportAsync(exported, createMissingMasters: false, CancellationToken.None);

        result.ImportedRows.Should().Be(1);
        var reimported = await target.Products.SingleAsync();
        reimported.Name.Should().Be("Mouse");
        reimported.Barcode.Should().Be("1,23");
        reimported.Description.Should().Be("Quoted, description");
        reimported.TrackExpiry.Should().BeTrue();
    }
}
