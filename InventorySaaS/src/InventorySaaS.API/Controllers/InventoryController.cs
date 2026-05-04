using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Services;
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

    [HttpPost("stock-in")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> StockIn([FromBody] StockInRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.StockInAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("stock-out")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> StockOut([FromBody] StockOutRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.StockOutAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("transfer")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Transfer([FromBody] StockTransferRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.TransferAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("adjustment")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _inventoryService.AdjustAsync(request, cancellationToken);
        return Ok(result);
    }
}
