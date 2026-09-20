using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService) => _inventoryService = inventoryService;

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _inventoryService.GetBalancesAsync(pagination, warehouseId, productId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _inventoryService.GetTransactionsAsync(pagination, warehouseId, productId, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Receives stock. Send an <c>Idempotency-Key</c> header from scanning clients: a repeated
    /// key returns the original transaction rather than posting a second one, so a double-scan
    /// or a retry over flaky warehouse Wi-Fi cannot duplicate stock.
    /// </summary>
    [HttpPost("stock-in")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> StockIn([FromBody] StockInRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.StockInAsync(request, cancellationToken, IdempotencyKey());
        return Ok(result);
    }

    [HttpPost("stock-out")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> StockOut([FromBody] StockOutRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.StockOutAsync(request, cancellationToken, IdempotencyKey());
        return Ok(result);
    }

    [HttpPost("transfer")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Transfer([FromBody] StockTransferRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.TransferAsync(request, cancellationToken, IdempotencyKey());
        return Ok(result);
    }

    [HttpPost("adjustment")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.AdjustAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reads the optional <c>Idempotency-Key</c> header. Absent or blank means "no replay
    /// protection", which keeps the existing non-scanning screens working unchanged.
    /// </summary>
    private string? IdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values)) return null;

        var key = values.ToString().Trim();
        if (key.Length == 0) return null;

        // Matches the column width; a longer value is a client bug, not a usable key.
        return key.Length > MaxIdempotencyKeyLength
            ? throw new BadRequestException($"Idempotency-Key must be {MaxIdempotencyKeyLength} characters or fewer.")
            : key;
    }

    private const int MaxIdempotencyKeyLength = 128;
}
