using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class SupplierPaymentsController : ControllerBase
{
    private readonly ISupplierPaymentService _paymentService;

    public SupplierPaymentsController(ISupplierPaymentService paymentService) => _paymentService = paymentService;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _paymentService.GetAllAsync(pagination, supplierId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _paymentService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _paymentService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
