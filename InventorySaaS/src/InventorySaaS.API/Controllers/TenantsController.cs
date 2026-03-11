using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Tenants.Commands;
using InventorySaaS.Application.Features.Tenants.DTOs;
using InventorySaaS.Application.Features.Tenants.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TenantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TenantsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _mediator.Send(new GetAllTenantsQuery(pagination));
        return Ok(result.Value);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await _mediator.Send(new GetTenantQuery());
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPut("current")]
    [Authorize(Policy = "TenantAdminOnly")]
    public async Task<IActionResult> Update([FromBody] UpdateTenantRequest request)
    {
        var result = await _mediator.Send(new UpdateTenantCommand(
            request.Name,
            request.ContactEmail,
            request.ContactPhone,
            request.Address,
            request.City,
            request.Country,
            request.Currency,
            request.Timezone,
            request.LogoUrl));
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Errors);
    }
}
