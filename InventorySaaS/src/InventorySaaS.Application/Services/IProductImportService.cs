using InventorySaaS.Application.Features.Products.DTOs;

namespace InventorySaaS.Application.Services;

public interface IProductImportService
{
    /// <summary>
    /// Validates a CSV without writing anything, so the user sees exactly what will happen.
    /// </summary>
    Task<ProductImportResult> PreviewAsync(
        string csvContent,
        bool createMissingMasters,
        CancellationToken cancellationToken);

    /// <summary>
    /// Imports every valid row and reports the rest. Invalid rows are skipped, never half-applied;
    /// the valid set is written in a single save, so it lands all together or not at all.
    /// </summary>
    Task<ProductImportResult> ImportAsync(
        string csvContent,
        bool createMissingMasters,
        CancellationToken cancellationToken);

    /// <summary>A CSV holding just the header row and one example, to start from.</summary>
    byte[] BuildTemplate();

    /// <summary>Exports the tenant's products in exactly the shape the importer accepts.</summary>
    Task<byte[]> ExportAsync(CancellationToken cancellationToken);
}
