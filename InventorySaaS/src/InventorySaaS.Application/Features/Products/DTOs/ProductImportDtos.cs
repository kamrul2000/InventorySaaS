namespace InventorySaaS.Application.Features.Products.DTOs;

/// <summary>What the importer decided about one CSV row.</summary>
public enum ProductImportRowStatus
{
    /// <summary>Passed validation; will be (or was) created.</summary>
    Valid,

    /// <summary>Failed validation; reported and skipped, never partially applied.</summary>
    Invalid,

    /// <summary>Passed validation and was written to the database.</summary>
    Imported
}

public record ProductImportRow(
    /// <summary>1-based line number in the uploaded file, header included, so it matches what a spreadsheet shows.</summary>
    int LineNumber,
    string? Name,
    string? Sku,
    string? CategoryName,
    string? BrandName,
    string? UnitName,
    string Status,
    IReadOnlyList<string> Errors);

public record ProductImportResult(
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int ImportedRows,
    /// <summary>True when nothing was written — the preview pass.</summary>
    bool IsPreview,
    /// <summary>Master records the import would create (preview) or did create (commit).</summary>
    IReadOnlyList<string> NewCategories,
    IReadOnlyList<string> NewBrands,
    IReadOnlyList<string> NewUnits,
    IReadOnlyList<ProductImportRow> Rows);
