using InventorySaaS.Application.Features.Products.DTOs;

namespace InventorySaaS.Application.Interfaces;

/// <summary>
/// Extracts structured product information from an uploaded product photo
/// (e.g. label, packaging, shelf shot) using a vision model. The result is
/// intended as a draft that the user reviews and edits before submission to
/// <c>POST /api/v1/Products</c>; nothing is persisted by this service.
/// </summary>
public interface IProductExtractionService
{
    /// <summary>
    /// Reads the supplied image stream and returns extracted product fields.
    /// </summary>
    /// <param name="imageStream">The raw image bytes. The caller owns the lifetime of the stream.</param>
    /// <param name="mimeType">The image MIME type, e.g. <c>image/jpeg</c> or <c>image/png</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A typed <see cref="ProductExtractionResult"/>. Fields the model is not confident about will be <c>null</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the upstream vision service is misconfigured or returns an unparseable response.</exception>
    Task<ProductExtractionResult> ExtractFromImageAsync(
        Stream imageStream,
        string mimeType,
        CancellationToken cancellationToken = default);
}
