using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.Commands;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Features.Inventory.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class InventoryController : ControllerBase
{
    private readonly IMediator _mediator;

    public InventoryController(IMediator mediator) => _mediator = mediator;

    [HttpGet("balances")]
    public async Task<IActionResult> GetBalances(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetInventoryBalancesQuery(pagination, warehouseId, productId));
        return Ok(result.Value);
    }

    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] Guid? warehouseId = null,
        [FromQuery] Guid? productId = null,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetInventoryTransactionsQuery(pagination, warehouseId, productId));
        return Ok(result.Value);
    }

    [HttpPost("stock-in")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> StockIn([FromBody] StockInRequest request)
    {
        var result = await _mediator.Send(new StockInCommand(
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            request.Quantity,
            request.UnitCost,
            request.BatchNumber,
            request.LotNumber,
            request.ExpiryDate,
            request.Notes));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("stock-out")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> StockOut([FromBody] StockOutRequest request)
    {
        var result = await _mediator.Send(new StockOutCommand(
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            request.Quantity,
            request.Notes));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("transfer")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Transfer([FromBody] StockTransferRequest request)
    {
        var result = await _mediator.Send(new StockTransferCommand(
            request.ProductId,
            request.SourceWarehouseId,
            request.SourceLocationId,
            request.DestinationWarehouseId,
            request.DestinationLocationId,
            request.Quantity,
            request.Notes));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("adjustment")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Adjust([FromBody] StockAdjustmentRequest request)
    {
        var result = await _mediator.Send(new StockAdjustmentCommand(
            request.ProductId,
            request.WarehouseId,
            request.LocationId,
            request.NewQuantity,
            request.Reason));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }
}
