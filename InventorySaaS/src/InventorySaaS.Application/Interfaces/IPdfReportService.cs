using InventorySaaS.Application.Features.Reports.DTOs;

namespace InventorySaaS.Application.Interfaces;

public interface IPdfReportService
{
    byte[] GenerateStockSummaryPdf(List<StockSummaryReportDto> data, string tenantName);
    byte[] GenerateLowStockPdf(List<LowStockReportDto> data, string tenantName);
    byte[] GenerateExpiryPdf(List<ExpiryReportDto> data, string tenantName);
    byte[] GenerateInventoryValuationPdf(List<InventoryValuationDto> data, string tenantName);
}
