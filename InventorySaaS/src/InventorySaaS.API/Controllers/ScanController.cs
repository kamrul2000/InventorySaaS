using InventorySaaS.Application.Features.Scan.DTOs;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

/// <summary>
/// Barcode and QR resolution for the scanning screens.
///
/// Everything here is read-only, so it sits at <c>ViewerUp</c>; the inventory-changing scan
/// operations post to the existing <c>Inventory</c>, <c>SalesOrders</c> and <c>StockCounts</c>
/// endpoints, which keep their own StaffUp/ManagerUp policies.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class ScanController : ControllerBase
{
    /// <summary>Guards against a malformed client sending a huge body as a "barcode".</summary>
    private const int MaxRawValueLength = 4096;

    private readonly IScanService _scanService;
    private readonly ILogger<ScanController> _logger;

    public ScanController(IScanService scanService, ILogger<ScanController> logger)
    {
        _scanService = scanService;
        _logger = logger;
    }

    /// <summary>
    /// Identifies what a scanned string points at: a product, variant, serial number, warehouse
    /// location, warehouse, sales order or purchase order.
    /// </summary>
    /// <remarks>
    /// A value that matches nothing returns 200 with <c>kind: "Unknown"</c> and
    /// <c>matched: false</c> — the scanner UI shows the message and stays ready for the next
    /// scan rather than treating an unknown label as a failed request.
    /// </remarks>
    [HttpPost("resolve")]
    public async Task<IActionResult> Resolve(
        [FromBody] ResolveScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RawValue))
            return BadRequest(new { error = "A scanned value is required." });

        if (request.RawValue.Length > MaxRawValueLength)
            return BadRequest(new { error = "The scanned value is too long to be a barcode." });

        var result = await _scanService.ResolveAsync(request, cancellationToken);

        if (!result.Matched)
            _logger.LogInformation("Scan did not resolve (kindsRequested={Kinds})", request.ExpectedKinds?.Count ?? 0);

        return Ok(result);
    }

    /// <summary>
    /// Scan-to-search: resolves a barcode, SKU or serial to a product and returns its stock
    /// broken down by warehouse, location, batch and expiry, plus serials on hand.
    /// </summary>
    [HttpGet("product")]
    public async Task<IActionResult> FindProduct(
        [FromQuery] string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "A barcode is required." });

        if (code.Length > MaxRawValueLength)
            return BadRequest(new { error = "The scanned value is too long to be a barcode." });

        return Ok(await _scanService.FindProductAsync(code, cancellationToken));
    }

    /// <summary>
    /// Stock for one product/warehouse/location/batch combination — the pre-submit availability
    /// check the stock-out and transfer screens show before letting staff post.
    /// </summary>
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability(
        [FromQuery] Guid productId,
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid? locationId = null,
        [FromQuery] string? batchNumber = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _scanService.GetAvailabilityAsync(
            productId, warehouseId, locationId, batchNumber, cancellationToken);

        return Ok(result);
    }
}
