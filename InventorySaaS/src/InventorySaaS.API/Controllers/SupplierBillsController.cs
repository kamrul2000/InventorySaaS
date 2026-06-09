using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Billing.DTOs;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class SupplierBillsController : ControllerBase
{
    private readonly ISupplierBillService _billService;

    public SupplierBillsController(ISupplierBillService billService) => _billService = billService;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? supplierId = null,
        [FromQuery] string? status = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _billService.GetAllAsync(pagination, supplierId, status, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _billService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("outstanding/{supplierId:guid}")]
    public async Task<IActionResult> GetOutstanding(Guid supplierId, CancellationToken cancellationToken)
    {
        var result = await _billService.GetOutstandingBySupplierAsync(supplierId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Create(
        [FromBody] CreateSupplierBillRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _billService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("from-purchase-order")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> CreateFromPurchaseOrder(
        [FromBody] CreateBillFromPurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _billService.CreateFromPurchaseOrderAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        var result = await _billService.ApproveAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _billService.CancelAsync(id, cancellationToken);
        return Ok(result);
    }
}
