using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.Commands;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Features.Warehouses.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class WarehousesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WarehousesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetWarehousesQuery(pagination));
        return Ok(result.Value);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetWarehouseByIdQuery(id));
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request)
    {
        var result = await _mediator.Send(new CreateWarehouseCommand(
            request.Name,
            request.Code,
            request.Address,
            request.City,
            request.Country,
            request.ContactPerson,
            request.ContactPhone,
            request.IsDefault));
        return result.IsSuccess ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value) : BadRequest(result.Errors);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWarehouseRequest request)
    {
        var result = await _mediator.Send(new UpdateWarehouseCommand(
            id,
            request.Name,
            request.Address,
            request.City,
            request.IsDefault,
            request.IsActive));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }

    [HttpPost("{warehouseId:guid}/locations")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> CreateLocation(Guid warehouseId, [FromBody] CreateLocationRequest request)
    {
        var result = await _mediator.Send(new CreateLocationCommand(
            warehouseId,
            request.Name,
            request.Aisle,
            request.Rack,
            request.Bin,
            request.Description));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }
}
