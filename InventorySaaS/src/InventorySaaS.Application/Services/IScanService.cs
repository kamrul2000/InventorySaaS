using InventorySaaS.Application.Features.Scan.DTOs;

namespace InventorySaaS.Application.Services;

public interface IScanService
{
    /// <summary>
    /// Decodes a scanned string and identifies what it points at — product, variant, serial,
    /// location, warehouse, or a sales/purchase order document. An unrecognised value comes back
    /// as <c>Unknown</c> rather than throwing, so the UI can say so and stay ready for the next scan.
    /// </summary>
    Task<ScanResultDto> ResolveAsync(ResolveScanRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Exact product lookup for the scan-to-search screen, with the product's stock broken down
    /// by warehouse, location and batch plus any serial numbers on hand.
    /// </summary>
    Task<ScanResultDto> FindProductAsync(string code, CancellationToken cancellationToken);

    /// <summary>
    /// Stock for one product/warehouse/location/batch combination, for the pre-submit
    /// availability check on stock-out and transfer screens.
    /// </summary>
    Task<ScanAvailabilityDto> GetAvailabilityAsync(
        Guid productId,
        Guid warehouseId,
        Guid? locationId,
        string? batchNumber,
        CancellationToken cancellationToken);
}
