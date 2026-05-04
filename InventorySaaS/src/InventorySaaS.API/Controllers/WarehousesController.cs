using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService) => _warehouseService = warehouseService;

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
        var result = await _warehouseService.GetAllAsync(pagination, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Create(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWarehouseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _warehouseService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpGet("{warehouseId:guid}/locations")]
    public async Task<IActionResult> GetLocations(Guid warehouseId, CancellationToken cancellationToken)
    {
        var result = await _warehouseService.GetLocationsAsync(warehouseId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{warehouseId:guid}/locations")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> CreateLocation(
        Guid warehouseId,
        [FromBody] CreateLocationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _warehouseService.CreateLocationAsync(warehouseId, request, cancellationToken);
        return Ok(result);
    }
}
