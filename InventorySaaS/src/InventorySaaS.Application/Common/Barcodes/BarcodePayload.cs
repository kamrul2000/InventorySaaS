namespace InventorySaaS.Application.Common.Barcodes;

/// <summary>
/// The structured fields carried by a scan, whatever shape the label used.
/// Every field is optional — a plain retail barcode fills only <see cref="Code"/>.
/// </summary>
/// <param name="Code">The primary identifier: a product barcode, SKU, location or document number.</param>
/// <param name="BatchNumber">Batch/lot, when the label carries one.</param>
/// <param name="SerialNumber">Serial number, when the label carries one.</param>
/// <param name="ExpiryDate">Expiry, when the label carries one.</param>
/// <param name="Quantity">Quantity encoded on the label (GS1 AI 30), when present.</param>
/// <param name="Format">Which encoding the raw value was recognised as, for diagnostics.</param>
public record BarcodePayload(
    string Code,
    string? BatchNumber = null,
    string? SerialNumber = null,
    DateTime? ExpiryDate = null,
    int? Quantity = null,
    BarcodePayloadFormat Format = BarcodePayloadFormat.Plain);

public enum BarcodePayloadFormat
{
    /// <summary>A bare value — the overwhelmingly common case for retail barcodes.</summary>
    Plain = 0,

    /// <summary>A JSON object QR code produced by this system's own label printing.</summary>
    Json = 1,

    /// <summary>A GS1 Application Identifier string, e.g. <c>(01)08801234567890(10)B2401(17)271231</c>.</summary>
    Gs1 = 2
}
