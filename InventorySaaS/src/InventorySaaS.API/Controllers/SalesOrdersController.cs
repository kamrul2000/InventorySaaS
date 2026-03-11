using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.Commands;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Features.SalesOrders.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class SalesOrdersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesOrdersController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetSalesOrdersQuery(pagination));
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetSalesOrderByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Create([FromBody] CreateSalesOrderRequest request)
    {
        var result = await _mediator.Send(new CreateSalesOrderCommand(
            request.CustomerId,
            request.WarehouseId,
            request.DeliveryDate,
            request.ShippingAddress,
            request.Notes,
            request.Items));
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var result = await _mediator.Send(new ConfirmSalesOrderCommand(id));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("{id:guid}/deliver")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Deliver(Guid id, [FromBody] DeliverSalesOrderRequest request)
    {
        var result = await _mediator.Send(new DeliverSalesOrderCommand(
            id,
            request.Items,
            request.Notes));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }
}
