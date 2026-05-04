using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;
    private readonly IPdfReportService _pdfService;

    public ReportsController(IReportService reportService, IPdfReportService pdfService)
    {
        _reportService = reportService;
        _pdfService = pdfService;
    }

    [HttpGet("stock-summary")]
    public async Task<IActionResult> StockSummary(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _reportService.GetStockSummaryAsync(pagination, warehouseId, categoryId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("stock-summary/pdf")]
    public async Task<IActionResult> StockSummaryPdf(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(1, 10000, null, null, false);
        var result = await _reportService.GetStockSummaryAsync(pagination, warehouseId, categoryId, cancellationToken);

        var pdf = _pdfService.GenerateStockSummaryPdf(result.Items, "InventorySaaS");
        return File(pdf, "application/pdf", $"Stock_Summary_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _reportService.GetLowStockAsync(pagination, cancellationToken);
        return Ok(result);
    }

    [HttpGet("low-stock/pdf")]
    public async Task<IActionResult> LowStockPdf(CancellationToken cancellationToken)
    {
        var pagination = new PaginationParams(1, 10000, null, null, false);
        var result = await _reportService.GetLowStockAsync(pagination, cancellationToken);

        var pdf = _pdfService.GenerateLowStockPdf(result.Items, "InventorySaaS");
        return File(pdf, "application/pdf", $"Low_Stock_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("expiry")]
    public async Task<IActionResult> Expiry(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] int daysAhead = 30,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _reportService.GetExpiryAsync(pagination, daysAhead, cancellationToken);
        return Ok(result);
    }

    [HttpGet("expiry/pdf")]
    public async Task<IActionResult> ExpiryPdf([FromQuery] int daysAhead = 30, CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(1, 10000, null, null, false);
        var result = await _reportService.GetExpiryAsync(pagination, daysAhead, cancellationToken);

        var pdf = _pdfService.GenerateExpiryPdf(result.Items, "InventorySaaS");
        return File(pdf, "application/pdf", $"Expiry_Report_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("inventory-valuation")]
    public async Task<IActionResult> InventoryValuation(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetInventoryValuationAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("inventory-valuation/pdf")]
    public async Task<IActionResult> InventoryValuationPdf(CancellationToken cancellationToken)
    {
        var result = await _reportService.GetInventoryValuationAsync(cancellationToken);

        var pdf = _pdfService.GenerateInventoryValuationPdf(result, "InventorySaaS");
        return File(pdf, "application/pdf", $"Inventory_Valuation_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }
}
