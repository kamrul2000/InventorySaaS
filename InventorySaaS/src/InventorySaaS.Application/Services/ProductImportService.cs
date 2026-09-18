using System.Globalization;
using InventorySaaS.Application.Common.Csv;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class ProductImportService : IProductImportService
{
    /// <summary>Kept well under the request size limit; a bigger load belongs in a background job.</summary>
    private const int MaxRows = 2000;

    private const string ColName = "Name";
    private const string ColSku = "Sku";
    private const string ColBarcode = "Barcode";
    private const string ColCategory = "Category";
    private const string ColBrand = "Brand";
    private const string ColUnit = "Unit";
    private const string ColCostPrice = "CostPrice";
    private const string ColSellingPrice = "SellingPrice";
    private const string ColReorderLevel = "ReorderLevel";
    private const string ColTrackExpiry = "TrackExpiry";
    private const string ColDescription = "Description";

    private static readonly string[] Headers =
    [
        ColName, ColSku, ColBarcode, ColCategory, ColBrand, ColUnit,
        ColCostPrice, ColSellingPrice, ColReorderLevel, ColTrackExpiry, ColDescription
    ];

    private static readonly string[] RequiredHeaders =
        [ColName, ColCategory, ColUnit, ColCostPrice, ColSellingPrice];

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ProductImportService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public Task<ProductImportResult> PreviewAsync(
        string csvContent,
        bool createMissingMasters,
        CancellationToken cancellationToken) =>
        RunAsync(csvContent, createMissingMasters, commit: false, cancellationToken);

    public Task<ProductImportResult> ImportAsync(
        string csvContent,
        bool createMissingMasters,
        CancellationToken cancellationToken) =>
        RunAsync(csvContent, createMissingMasters, commit: true, cancellationToken);

    public byte[] BuildTemplate() => CsvFile.Build(
        Headers,
        [["Wireless Mouse", "", "1234567890123", "Electronics", "TechBrand", "Piece",
          "15.00", "29.99", "50", "false", "Optional description"]]);

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken)
    {
        var products = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var rows = products.Select(p => new string?[]
        {
            p.Name,
            p.Sku,
            p.Barcode,
            p.Category.Name,
            p.Brand?.Name,
            p.UnitOfMeasure.Name,
            p.CostPrice.ToString(CultureInfo.InvariantCulture),
            p.SellingPrice.ToString(CultureInfo.InvariantCulture),
            p.ReorderLevel.ToString(CultureInfo.InvariantCulture),
            p.TrackExpiry ? "true" : "false",
            p.Description
        });

        return CsvFile.Build(Headers, rows);
    }

    private async Task<ProductImportResult> RunAsync(
        string csvContent,
        bool createMissingMasters,
        bool commit,
        CancellationToken cancellationToken)
    {
        var parsed = CsvFile.Parse(csvContent);
        if (parsed.Count == 0)
            throw new BadRequestException("The file is empty.");

        var columns = MapColumns(parsed[0]);
        var dataRows = parsed.Skip(1).ToList();

        if (dataRows.Count == 0)
            throw new BadRequestException("The file contains a header but no data rows.");

        if (dataRows.Count > MaxRows)
            throw new BadRequestException($"The file has {dataRows.Count} rows; the limit is {MaxRows} per import.");

        var tenantId = _currentUserService.TenantId!.Value;
        var lookups = await LoadLookupsAsync(cancellationToken);

        var results = new List<ProductImportRow>(dataRows.Count);
        var pending = new List<PendingProduct>();

        for (var i = 0; i < dataRows.Count; i++)
        {
            // +2: the header occupies line 1 and spreadsheets count from 1.
            var lineNumber = i + 2;
            results.Add(ValidateRow(dataRows[i], lineNumber, columns, lookups, createMissingMasters, pending));
        }

        if (!commit)
        {
            return BuildResult(results, lookups, isPreview: true, importedRows: 0);
        }

        var imported = await PersistAsync(pending, lookups, tenantId, cancellationToken);

        // Rows that made it in are reported as Imported rather than merely Valid.
        var committed = results
            .Select(r => r.Status == nameof(ProductImportRowStatus.Valid)
                ? r with { Status = nameof(ProductImportRowStatus.Imported) }
                : r)
            .ToList();

        return BuildResult(committed, lookups, isPreview: false, importedRows: imported);
    }

    private ProductImportResult BuildResult(
        List<ProductImportRow> rows,
        Lookups lookups,
        bool isPreview,
        int importedRows)
    {
        var invalid = rows.Count(r => r.Status == nameof(ProductImportRowStatus.Invalid));

        return new ProductImportResult(
            TotalRows: rows.Count,
            ValidRows: rows.Count - invalid,
            InvalidRows: invalid,
            ImportedRows: importedRows,
            IsPreview: isPreview,
            NewCategories: lookups.NewCategoryNames,
            NewBrands: lookups.NewBrandNames,
            NewUnits: lookups.NewUnitNames,
            Rows: rows);
    }

    /// <summary>
    /// Maps header names to positions so columns may appear in any order, and rejects the file
    /// up front when a required one is missing — better than failing every row for the same reason.
    /// </summary>
    private static Dictionary<string, int> MapColumns(List<string> headerRow)
    {
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < headerRow.Count; i++)
        {
            var name = headerRow[i].Trim();
            if (name.Length > 0 && !columns.ContainsKey(name))
                columns[name] = i;
        }

        var missing = RequiredHeaders.Where(h => !columns.ContainsKey(h)).ToList();
        if (missing.Count > 0)
            throw new BadRequestException($"The file is missing required column(s): {string.Join(", ", missing)}.");

        return columns;
    }

    private async Task<Lookups> LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(cancellationToken);

        var brands = await _context.Brands
            .Select(b => new { b.Id, b.Name })
            .ToListAsync(cancellationToken);

        var units = await _context.UnitsOfMeasure
            .Select(u => new { u.Id, u.Name, u.Abbreviation })
            .ToListAsync(cancellationToken);

        var tenantId = _currentUserService.TenantId!.Value;

        // The unique index on (TenantId, Sku) covers soft-deleted rows too, so a SKU freed by a
        // deleted product is still taken as far as the database is concerned.
        var skus = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId)
            .Select(p => p.Sku)
            .ToListAsync(cancellationToken);

        return new Lookups(
            categories.ToDictionary(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase),
            brands.ToDictionary(b => b.Name, b => b.Id, StringComparer.OrdinalIgnoreCase),
            units.ToDictionary(u => u.Name, u => u.Id, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(units.Select(u => u.Abbreviation), StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(skus, StringComparer.OrdinalIgnoreCase));
    }

    private ProductImportRow ValidateRow(
        List<string> row,
        int lineNumber,
        Dictionary<string, int> columns,
        Lookups lookups,
        bool createMissingMasters,
        List<PendingProduct> pending)
    {
        var errors = new List<string>();

        var name = Field(row, columns, ColName);
        var sku = Field(row, columns, ColSku);
        var categoryName = Field(row, columns, ColCategory);
        var brandName = Field(row, columns, ColBrand);
        var unitName = Field(row, columns, ColUnit);

        if (string.IsNullOrWhiteSpace(name))
            errors.Add("Name is required.");

        var categoryId = ResolveMaster(
            categoryName, "Category", required: true, lookups.Categories,
            createMissingMasters, lookups.NewCategoryNames, errors);

        var unitId = ResolveMaster(
            unitName, "Unit", required: true, lookups.Units,
            createMissingMasters, lookups.NewUnitNames, errors);

        var brandId = ResolveMaster(
            brandName, "Brand", required: false, lookups.Brands,
            createMissingMasters, lookups.NewBrandNames, errors);

        var costPrice = ParseDecimal(Field(row, columns, ColCostPrice), ColCostPrice, required: true, errors);
        var sellingPrice = ParseDecimal(Field(row, columns, ColSellingPrice), ColSellingPrice, required: true, errors);
        var reorderLevel = ParseInt(Field(row, columns, ColReorderLevel), ColReorderLevel, errors);
        var trackExpiry = ParseBool(Field(row, columns, ColTrackExpiry), ColTrackExpiry, errors);

        if (!string.IsNullOrWhiteSpace(sku))
        {
            sku = sku.Trim();
            if (sku.Length > 50)
                errors.Add("Sku exceeds 50 characters.");
            else if (!lookups.Skus.Add(sku))
                errors.Add($"Sku '{sku}' already exists (in the system or earlier in this file).");
        }

        if (errors.Count > 0)
        {
            return new ProductImportRow(
                lineNumber, name, sku, categoryName, brandName, unitName,
                nameof(ProductImportRowStatus.Invalid), errors);
        }

        pending.Add(new PendingProduct(
            Name: name!.Trim(),
            Sku: sku,
            Barcode: Field(row, columns, ColBarcode)?.Trim(),
            Description: Field(row, columns, ColDescription)?.Trim(),
            CategoryKey: categoryId!,
            BrandKey: brandId,
            UnitKey: unitId!,
            CostPrice: costPrice!.Value,
            SellingPrice: sellingPrice!.Value,
            ReorderLevel: reorderLevel ?? 0,
            TrackExpiry: trackExpiry ?? false));

        return new ProductImportRow(
            lineNumber, name, sku, categoryName, brandName, unitName,
            nameof(ProductImportRowStatus.Valid), []);
    }

    /// <summary>
    /// Resolves a master record by name. Unknown names are an error unless the caller opted into
    /// creating them, which keeps a typo from quietly spawning a new category on every import.
    /// </summary>
    private static MasterKey? ResolveMaster(
        string? name,
        string label,
        bool required,
        Dictionary<string, Guid> existing,
        bool createMissing,
        List<string> newNames,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (required) errors.Add($"{label} is required.");
            return null;
        }

        var trimmed = name.Trim();

        if (existing.TryGetValue(trimmed, out var id))
            return MasterKey.Existing(id);

        if (!createMissing)
        {
            errors.Add($"{label} '{trimmed}' does not exist. Create it first, or enable \"create missing\".");
            return null;
        }

        if (!newNames.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            newNames.Add(trimmed);

        return MasterKey.New(trimmed);
    }

    private async Task<int> PersistAsync(
        List<PendingProduct> pending,
        Lookups lookups,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (pending.Count == 0) return 0;

        // Masters first: the products reference them, and the ids only exist once they are created.
        var categoryIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var categoryName in lookups.NewCategoryNames)
        {
            var category = new Category { TenantId = tenantId, Name = categoryName, IsActive = true };
            _context.Categories.Add(category);
            categoryIds[categoryName] = category.Id;
        }

        var brandIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var brandName in lookups.NewBrandNames)
        {
            var brand = new Brand { TenantId = tenantId, Name = brandName, IsActive = true };
            _context.Brands.Add(brand);
            brandIds[brandName] = brand.Id;
        }

        var unitIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var unitName in lookups.NewUnitNames)
        {
            var unit = new UnitOfMeasure
            {
                TenantId = tenantId,
                Name = unitName,
                Abbreviation = UniqueAbbreviation(unitName, lookups.Abbreviations),
                IsActive = true
            };
            _context.UnitsOfMeasure.Add(unit);
            unitIds[unitName] = unit.Id;
        }

        // Category names drive the SKU prefix, so resolve every name before allocating any SKU.
        var categoryNamesById = await _context.Categories
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        foreach (var (name, id) in categoryIds)
            categoryNamesById[id] = name;

        var skuAllocator = new SkuAllocator(lookups.Skus);

        foreach (var item in pending)
        {
            var categoryId = item.CategoryKey.Resolve(categoryIds);
            var unitId = item.UnitKey.Resolve(unitIds);
            var brandId = item.BrandKey?.Resolve(brandIds);

            var sku = string.IsNullOrWhiteSpace(item.Sku)
                ? skuAllocator.Next(categoryNamesById[categoryId])
                : item.Sku;

            _context.Products.Add(new ProductInfo
            {
                TenantId = tenantId,
                Name = item.Name,
                Description = item.Description,
                Sku = sku,
                Barcode = string.IsNullOrWhiteSpace(item.Barcode) ? null : item.Barcode,
                CategoryId = categoryId,
                BrandId = brandId,
                UnitOfMeasureId = unitId,
                CostPrice = item.CostPrice,
                SellingPrice = item.SellingPrice,
                ReorderLevel = item.ReorderLevel,
                TrackExpiry = item.TrackExpiry,
                IsActive = true
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }

    /// <summary>
    /// Mirrors <see cref="UnitOfMeasureService"/>'s derivation, then disambiguates: importing
    /// "Piece" and "Pieces" together would otherwise produce "pie" twice.
    /// </summary>
    private static string UniqueAbbreviation(string name, HashSet<string> taken)
    {
        var baseAbbreviation = name.Length >= 3
            ? name[..3].ToLowerInvariant()
            : name.ToLowerInvariant();

        var candidate = baseAbbreviation;
        var suffix = 2;
        while (!taken.Add(candidate))
        {
            candidate = $"{baseAbbreviation}{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static string? Field(List<string> row, Dictionary<string, int> columns, string column)
    {
        if (!columns.TryGetValue(column, out var index)) return null;
        return index < row.Count ? row[index] : null;
    }

    private static decimal? ParseDecimal(string? raw, string column, bool required, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (required) errors.Add($"{column} is required.");
            return null;
        }

        if (!decimal.TryParse(raw.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add($"{column} '{raw.Trim()}' is not a valid number.");
            return null;
        }

        if (value < 0)
        {
            errors.Add($"{column} cannot be negative.");
            return null;
        }

        return value;
    }

    private static int? ParseInt(string? raw, string column, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            errors.Add($"{column} '{raw.Trim()}' is not a whole number.");
            return null;
        }

        if (value < 0)
        {
            errors.Add($"{column} cannot be negative.");
            return null;
        }

        return value;
    }

    private static bool? ParseBool(string? raw, string column, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        return raw.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "y" or "1" => true,
            "false" or "no" or "n" or "0" => false,
            _ => Invalid()
        };

        bool? Invalid()
        {
            errors.Add($"{column} '{raw.Trim()}' is not a yes/no value.");
            return null;
        }
    }

    /// <summary>
    /// A master record referenced by a row: either one that already exists, or one this import
    /// will create — whose id is only known after it is added.
    /// </summary>
    private sealed record MasterKey(Guid? Id, string? NewName)
    {
        public static MasterKey Existing(Guid id) => new(id, null);
        public static MasterKey New(string name) => new(null, name);

        public Guid Resolve(Dictionary<string, Guid> created) =>
            Id ?? created[NewName!];
    }

    private sealed record PendingProduct(
        string Name,
        string? Sku,
        string? Barcode,
        string? Description,
        MasterKey CategoryKey,
        MasterKey? BrandKey,
        MasterKey UnitKey,
        decimal CostPrice,
        decimal SellingPrice,
        int ReorderLevel,
        bool TrackExpiry);

    private sealed record Lookups(
        Dictionary<string, Guid> Categories,
        Dictionary<string, Guid> Brands,
        Dictionary<string, Guid> Units,
        HashSet<string> Abbreviations,
        HashSet<string> Skus)
    {
        public List<string> NewCategoryNames { get; } = [];
        public List<string> NewBrandNames { get; } = [];
        public List<string> NewUnitNames { get; } = [];
    }

    /// <summary>
    /// Hands out SKUs for a whole batch. The per-product query the single-create path uses would
    /// return the same number for every row here, because nothing is saved until the end.
    /// </summary>
    private sealed class SkuAllocator(HashSet<string> taken)
    {
        private readonly Dictionary<string, int> _next = new(StringComparer.OrdinalIgnoreCase);

        public string Next(string categoryName)
        {
            var code = categoryName.Length >= 3
                ? categoryName[..3].ToUpperInvariant()
                : categoryName.ToUpperInvariant().PadRight(3, 'X');

            var prefix = $"{code}-";

            if (!_next.TryGetValue(prefix, out var number))
            {
                number = 1;
                foreach (var existing in taken)
                {
                    if (existing.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(existing[prefix.Length..], out var n)
                        && n >= number)
                    {
                        number = n + 1;
                    }
                }
            }

            string candidate;
            do
            {
                candidate = $"{prefix}{number:D5}";
                number++;
            }
            while (!taken.Add(candidate));

            _next[prefix] = number;
            return candidate;
        }
    }
}
