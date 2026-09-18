using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Units.DTOs;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class UnitsOfMeasureController : ControllerBase
{
    private readonly IUnitOfMeasureService _unitService;

    public UnitsOfMeasureController(IUnitOfMeasureService unitService) => _unitService = unitService;

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
        var result = await _unitService.GetAllAsync(pagination, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _unitService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Create(
        [FromBody] CreateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _unitService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateUnitOfMeasureRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _unitService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _unitService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
