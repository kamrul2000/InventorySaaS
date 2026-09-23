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
        Guid? warehouseId,
        CancellationToken cancellationToken);

    Task<PaginatedList<ExpiryReportDto>> GetExpiryAsync(
        PaginationParams pagination,
        int daysAhead,
        Guid? warehouseId,
        CancellationToken cancellationToken);

    Task<List<InventoryValuationDto>> GetInventoryValuationAsync(CancellationToken cancellationToken);

    /// <summary>Outstanding customer invoices, bucketed by how overdue they are.</summary>
    Task<List<AgingReportDto>> GetArAgingAsync(DateTime asOf, CancellationToken cancellationToken);

    /// <summary>Outstanding supplier bills, bucketed by how overdue they are.</summary>
    Task<List<AgingReportDto>> GetApAgingAsync(DateTime asOf, CancellationToken cancellationToken);

    Task<List<SalesSummaryDto>> GetSalesSummaryAsync(
        DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);

    Task<List<PurchaseSummaryDto>> GetPurchaseSummaryAsync(
        DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);

    Task<List<ProfitabilityDto>> GetProfitabilityAsync(
        DateTime? startDate, DateTime? endDate, CancellationToken cancellationToken);
}
