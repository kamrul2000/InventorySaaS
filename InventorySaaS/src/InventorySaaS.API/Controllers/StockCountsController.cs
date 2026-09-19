using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.StockCounts.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

/// <summary>
/// Physical stock counting.
///
/// Counting is Staff work and changes nothing; <c>approve</c> is the Manager gate that turns
/// the counted variances into inventory adjustments, so it alone requires <c>ManagerUp</c>.
/// </summary>
[ApiController]
[Route("api/v1/stock-counts")]
[Authorize(Policy = "ViewerUp")]
public class StockCountsController : ControllerBase
{
    private const int MaxIdempotencyKeyLength = 128;

    private readonly IStockCountService _stockCountService;

    public StockCountsController(IStockCountService stockCountService) =>
        _stockCountService = stockCountService;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, null, false);
        var result = await _stockCountService.GetAllAsync(pagination, warehouseId, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await _stockCountService.GetByIdAsync(id, cancellationToken));

    /// <summary>Opens a counting run over a warehouse, optionally narrowed to one bin.</summary>
    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Start(
        [FromBody] StartStockCountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _stockCountService.StartAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Records a counted item. Repeat scans of the same product, bin and batch add up; send
    /// <c>setExactQuantity: true</c> to replace the running total instead.
    /// </summary>
    [HttpPost("{id:guid}/scan")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Scan(
        Guid id,
        [FromBody] StockCountScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A scan payload is required." });

        var result = await _stockCountService.ScanAsync(id, request, cancellationToken, IdempotencyKey());
        return Ok(result);
    }

    /// <summary>Hands the count to a Manager for review; counting stops here.</summary>
    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Submit(
        Guid id,
        [FromBody] SubmitStockCountRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _stockCountService.SubmitAsync(
            id, request ?? new SubmitStockCountRequest(null), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Approves the variances and posts the adjustments. Manager and above only — this is the
    /// step that changes stock.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Approve(
        Guid id,
        [FromBody] ApproveStockCountRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _stockCountService.ApproveAsync(
            id, request ?? new ApproveStockCountRequest(null), cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _stockCountService.CancelAsync(id, cancellationToken));

    private string? IdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values)) return null;

        var key = values.ToString().Trim();
        if (key.Length == 0) return null;

        return key.Length > MaxIdempotencyKeyLength
            ? throw new BadRequestException($"Idempotency-Key must be {MaxIdempotencyKeyLength} characters or fewer.")
            : key;
    }
}
