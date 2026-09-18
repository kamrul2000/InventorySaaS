using InventorySaaS.Application.Features.Reports.DTOs;

namespace InventorySaaS.Application.Interfaces;

public interface IPdfReportService
{
    byte[] GenerateStockSummaryPdf(List<StockSummaryReportDto> data, string tenantName);
    byte[] GenerateLowStockPdf(List<LowStockReportDto> data, string tenantName);
    byte[] GenerateExpiryPdf(List<ExpiryReportDto> data, string tenantName);
    byte[] GenerateInventoryValuationPdf(List<InventoryValuationDto> data, string tenantName);
    byte[] GenerateAgingPdf(List<AgingReportDto> data, string tenantName, bool isReceivable, DateTime asOf);
    byte[] GenerateSalesSummaryPdf(List<SalesSummaryDto> data, string tenantName);
    byte[] GeneratePurchaseSummaryPdf(List<PurchaseSummaryDto> data, string tenantName);
    byte[] GenerateProfitabilityPdf(List<ProfitabilityDto> data, string tenantName);
}
