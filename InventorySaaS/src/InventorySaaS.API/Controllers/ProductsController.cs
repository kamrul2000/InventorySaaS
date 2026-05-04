using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Products.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventorySaaS.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize(Policy = "ViewerUp")]
public class ProductsController : ControllerBase
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedImageMimeTypes =
        new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png" };

    private readonly IProductService _productService;
    private readonly IProductExtractionService _extractionService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IProductService productService,
        IProductExtractionService extractionService,
        ILogger<ProductsController> logger)
    {
        _productService = productService;
        _extractionService = extractionService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? brandId = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = false,
        CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationParams(pageNumber, pageSize, search, sortBy, sortDescending);
        var result = await _productService.GetAllAsync(pagination, categoryId, brandId, isActive, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "StaffUp")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "ManagerUp")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Accepts a single product image (JPEG/PNG, ≤ 5 MB) and returns extracted product
    /// fields for the user to review and edit before submitting to <c>POST /api/v1/Products</c>.
    /// Nothing is persisted by this endpoint.
    /// </summary>
    [HttpPost("extract-from-image")]
    [Authorize(Policy = "StaffUp")]
    [RequestSizeLimit(MaxImageBytes + 32 * 1024)]
    public async Task<IActionResult> ExtractFromImage(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "An image file is required." });

        if (file.Length > MaxImageBytes)
            return BadRequest(new { error = $"Image exceeds the {MaxImageBytes / (1024 * 1024)} MB limit." });

        if (!AllowedImageMimeTypes.Contains(file.ContentType))
            return BadRequest(new { error = "Only image/jpeg and image/png are accepted." });

        _logger.LogInformation(
            "Product extraction requested (fileName={FileName}, contentType={ContentType}, sizeBytes={Size})",
            file.FileName, file.ContentType, file.Length);

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _extractionService.ExtractFromImageAsync(stream, file.ContentType, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Product extraction failed.");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
