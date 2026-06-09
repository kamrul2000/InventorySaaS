using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class SalesOrdersController : ControllerBase
{
    private readonly ISalesOrderService _salesOrderService;

    public SalesOrdersController(ISalesOrderService salesOrderService) => _salesOrderService = salesOrderService;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _salesOrderService.GetAllAsync(pagination, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.ConfirmAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Deliver(
        Guid id,
        [FromBody] DeliverSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.DeliverAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/return")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Return(
        Guid id,
        [FromBody] ReturnSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.ReturnAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _salesOrderService.CancelAsync(id, cancellationToken);
        return Ok(result);
    }
}
