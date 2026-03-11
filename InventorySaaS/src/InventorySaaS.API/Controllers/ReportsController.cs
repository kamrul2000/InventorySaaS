using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.Queries;
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

    public ReportsController(IMediator mediator) => _mediator = mediator;

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

    [HttpGet("inventory-valuation")]
    public async Task<IActionResult> InventoryValuation()
    {
        var result = await _mediator.Send(new GetInventoryValuationQuery());
        return Ok(result.Value);
    }
}
