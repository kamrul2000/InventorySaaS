using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.StockCounts.DTOs;

namespace InventorySaaS.Application.Services;

public interface IStockCountService
{
    Task<PaginatedList<StockCountSessionDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        string? status,
        CancellationToken cancellationToken);

    Task<StockCountSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Opens a counting run over a warehouse, optionally narrowed to one bin.</summary>
    Task<StockCountSessionDto> StartAsync(
        StartStockCountRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a counted item. Repeat scans of the same product, bin and batch accumulate onto
    /// one line; the system quantity is snapshotted when that line is first opened.
    /// </summary>
    Task<StockCountScanResultDto> ScanAsync(
        Guid id,
        StockCountScanRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null);

    /// <summary>Hands the count to a Manager for review. Counting stops at this point.</summary>
    Task<StockCountSessionDto> SubmitAsync(
        Guid id,
        SubmitStockCountRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Manager approval. Writes one adjustment transaction per varying line, bringing the
    /// balances to the counted quantities. This is the only step that changes stock.
    /// </summary>
    Task<StockCountSessionDto> ApproveAsync(
        Guid id,
        ApproveStockCountRequest request,
        CancellationToken cancellationToken);

    Task<StockCountSessionDto> CancelAsync(Guid id, CancellationToken cancellationToken);
}
