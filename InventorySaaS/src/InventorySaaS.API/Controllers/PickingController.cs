using InventorySaaS.Application.Features.Picking.DTOs;
using InventorySaaS.Application.Services;
using InventorySaaS.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

/// <summary>
/// Scan-driven picking for a sales order.
///
/// Picking is warehouse work, so the write operations sit at <c>StaffUp</c>, matching the
/// other inventory movements; reading progress is <c>ViewerUp</c> so supervisors can watch.
/// </summary>
[ApiController]
[Route("api/v1/sales-orders/{salesOrderId:guid}/picking")]
[Authorize(Policy = "ViewerUp")]
public class PickingController : ControllerBase
{
    private const int MaxIdempotencyKeyLength = 128;

    private readonly IPickingService _pickingService;

    public PickingController(IPickingService pickingService) => _pickingService = pickingService;

    /// <summary>
    /// Progress for the order's open picking session, with every line's requested, picked and
    /// remaining quantity. Returns 204 when no session is open.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(Guid salesOrderId, CancellationToken cancellationToken)
    {
        var session = await _pickingService.GetAsync(salesOrderId, cancellationToken);
        return session is null ? NoContent() : Ok(session);
    }

    /// <summary>Opens a picking run. Returns the existing session if one is already open.</summary>
    [HttpPost("start")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Start(
        Guid salesOrderId,
        [FromBody] StartPickRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _pickingService.StartAsync(
            salesOrderId, request ?? new StartPickRequest(null), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Records one pick. The body names either a product or the scanned barcode. Send an
    /// <c>Idempotency-Key</c> so a double-scan does not count the item twice.
    /// </summary>
    [HttpPost("scan")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Scan(
        Guid salesOrderId,
        [FromBody] PickScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { error = "A scan payload is required." });

        var result = await _pickingService.ScanAsync(
            salesOrderId, request, cancellationToken, IdempotencyKey());

        return Ok(result);
    }

    /// <summary>
    /// Closes the session once every line is fully picked. With <c>deliver: true</c> the picked
    /// quantities are handed to the order's delivery path, which is what moves the stock.
    /// </summary>
    [HttpPost("complete")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Complete(
        Guid salesOrderId,
        [FromBody] CompletePickRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await _pickingService.CompleteAsync(
            salesOrderId, request ?? new CompletePickRequest(), cancellationToken);

        return Ok(result);
    }

    /// <summary>Abandons the run and clears the picked quantities it recorded.</summary>
    [HttpPost("cancel")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Cancel(Guid salesOrderId, CancellationToken cancellationToken)
    {
        var result = await _pickingService.CancelAsync(salesOrderId, cancellationToken);
        return Ok(result);
    }

    private string? IdempotencyKey()
    {
        if (!Request.Headers.TryGetValue("Idempotency-Key", out var values)) return null;

        var key = values.ToString().Trim();
        if (key.Length == 0) return null;

        return key.Length > MaxIdempotencyKeyLength
            ? throw new BadRequestException($"Idempotency-Key must be {MaxIdempotencyKeyLength} characters or fewer.")
            : key;
    }
}
