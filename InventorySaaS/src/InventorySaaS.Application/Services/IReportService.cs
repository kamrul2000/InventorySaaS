using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;

namespace InventorySaaS.Application.Services;

public interface IReportService
{
    Task<PaginatedList<StockSummaryReportDto>> GetStockSummaryAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        Guid? categoryId,
        CancellationToken cancellationToken);

    Task<PaginatedList<LowStockReportDto>> GetLowStockAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken);

    Task<PaginatedList<ExpiryReportDto>> GetExpiryAsync(
        PaginationParams pagination,
        int daysAhead,
        CancellationToken cancellationToken);

    Task<List<InventoryValuationDto>> GetInventoryValuationAsync(CancellationToken cancellationToken);
}
