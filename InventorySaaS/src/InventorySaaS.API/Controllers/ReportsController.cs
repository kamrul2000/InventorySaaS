using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.Queries;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IPdfReportService _pdfService;

    public ReportsController(IMediator mediator, IPdfReportService pdfService)
    {
        _mediator = mediator;
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
        [FromQuery] Guid? categoryId = null)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetStockSummaryQuery(pagination, warehouseId, categoryId));
        return Ok(result.Value);
    }

    [HttpGet("stock-summary/pdf")]
    public async Task<IActionResult> StockSummaryPdf(
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? categoryId = null)
    {
        var pagination = new PaginationParams(1, 10000, null, null, false);
        var result = await _mediator.Send(new GetStockSummaryQuery(pagination, warehouseId, categoryId));
        if (!result.IsSuccess) return BadRequest(result.Errors);

        var pdf = _pdfService.GenerateStockSummaryPdf(result.Value!.Items, "InventorySaaS");
        return File(pdf, "application/pdf", $"Stock_Summary_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("low-stock")]
    public async Task<IActionResult> LowStock(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetLowStockReportQuery(pagination));
        return Ok(result.Value);
    }

    [HttpGet("low-stock/pdf")]
    public async Task<IActionResult> LowStockPdf()
    {
        var pagination = new PaginationParams(1, 10000, null, null, false);
        var result = await _mediator.Send(new GetLowStockReportQuery(pagination));
        if (!result.IsSuccess) return BadRequest(result.Errors);

        var pdf = _pdfService.GenerateLowStockPdf(result.Value!.Items, "InventorySaaS");
        return File(pdf, "application/pdf", $"Low_Stock_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("expiry")]
    public async Task<IActionResult> Expiry(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        [FromQuery] int daysAhead = 30)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetExpiryReportQuery(pagination, daysAhead));
        return Ok(result.Value);
    }

    [HttpGet("expiry/pdf")]
    public async Task<IActionResult> ExpiryPdf([FromQuery] int daysAhead = 30)
    {
        var pagination = new PaginationParams(1, 10000, null, null, false);
        var result = await _mediator.Send(new GetExpiryReportQuery(pagination, daysAhead));
        if (!result.IsSuccess) return BadRequest(result.Errors);

        var pdf = _pdfService.GenerateExpiryPdf(result.Value!.Items, "InventorySaaS");
        return File(pdf, "application/pdf", $"Expiry_Report_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    [HttpGet("inventory-valuation")]
    public async Task<IActionResult> InventoryValuation()
    {
        var result = await _mediator.Send(new GetInventoryValuationQuery());
        return Ok(result.Value);
    }

    [HttpGet("inventory-valuation/pdf")]
    public async Task<IActionResult> InventoryValuationPdf()
    {
        var result = await _mediator.Send(new GetInventoryValuationQuery());
        if (!result.IsSuccess) return BadRequest(result.Errors);

        var pdf = _pdfService.GenerateInventoryValuationPdf(result.Value!, "InventorySaaS");
        return File(pdf, "application/pdf", $"Inventory_Valuation_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }
}
