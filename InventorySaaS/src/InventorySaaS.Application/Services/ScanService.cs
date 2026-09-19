using InventorySaaS.Application.Common.Barcodes;
using InventorySaaS.Application.Features.Scan.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

/// <summary>
/// Resolves scanned barcodes and QR codes against tenant data.
///
/// Every query runs through <see cref="IApplicationDbContext"/>, so the global tenant filter
/// applies automatically — a scan can never surface another tenant's product, bin or order,
/// and there is no code path here that could opt out of it.
///
/// Lookups are exact matches, not the substring search the product list uses: a scan must
/// identify one thing or nothing. Exact matching relies on SQL Server's default case-insensitive
/// collation, so a manually typed lowercase SKU still resolves.
/// </summary>
public class ScanService : IScanService
{
    /// <summary>Caps how many serials ride along with a product lookup, to bound the payload.</summary>
    private const int MaxSerialsReturned = 100;

    private readonly IApplicationDbContext _context;

    public ScanService(IApplicationDbContext context) => _context = context;

    public async Task<ScanResultDto> ResolveAsync(
        ResolveScanRequest request,
        CancellationToken cancellationToken)
    {
        var payload = BarcodePayloadParser.Parse(request.RawValue);
        var raw = request.RawValue?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(payload.Code))
            return Unmatched(raw, payload, "Nothing was scanned.");

        var wanted = ParseExpectedKinds(request.ExpectedKinds);

        // Ordered most- to least-specific. Products come first because they are the
        // overwhelming majority of scans in a warehouse.
        foreach (var kind in new[]
                 {
                     ScanKind.Product, ScanKind.ProductVariant, ScanKind.Serial,
                     ScanKind.Location, ScanKind.Warehouse,
                     ScanKind.SalesOrder, ScanKind.PurchaseOrder
                 })
        {
            if (wanted is not null && !wanted.Contains(kind)) continue;

            var result = await TryResolveAsync(kind, payload, raw, cancellationToken);
            if (result is not null) return result;
        }

        return Unmatched(raw, payload, $"'{payload.Code}' is not assigned to anything in this workspace.");
    }

    public async Task<ScanResultDto> FindProductAsync(string code, CancellationToken cancellationToken)
    {
        var payload = BarcodePayloadParser.Parse(code);
        var raw = code?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(payload.Code))
            throw new BadRequestException("A barcode is required.");

        var result =
            await TryResolveAsync(ScanKind.Product, payload, raw, cancellationToken) ??
            await TryResolveAsync(ScanKind.ProductVariant, payload, raw, cancellationToken) ??
            await TryResolveAsync(ScanKind.Serial, payload, raw, cancellationToken);

        // A serial scan identifies a product too, so surface it the same way.
        if (result is { Kind: nameof(ScanKind.Serial), Serial: not null })
            result = result with { Product = await LoadProductAsync(result.Serial.ProductId, null, cancellationToken) };

        return result ?? Unmatched(raw, payload, $"'{payload.Code}' is not assigned to a product.");
    }

    public async Task<ScanAvailabilityDto> GetAvailabilityAsync(
        Guid productId,
        Guid warehouseId,
        Guid? locationId,
        string? batchNumber,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), productId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == warehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), warehouseId);

        var query = _context.InventoryBalances
            .Include(b => b.Location)
            .Where(b => b.ProductId == productId && b.WarehouseId == warehouseId);

        if (locationId.HasValue)
            query = query.Where(b => b.LocationId == locationId.Value);

        // A batch filter only narrows when one was asked for; null means "across all batches".
        if (!string.IsNullOrWhiteSpace(batchNumber))
            query = query.Where(b => b.BatchNumber == batchNumber);

        var balances = await query.ToListAsync(cancellationToken);

        var onHand = balances.Sum(b => b.QuantityOnHand);
        var reserved = balances.Sum(b => b.QuantityReserved);

        return new ScanAvailabilityDto(
            product.Id, product.Name, product.Sku,
            warehouse.Id, warehouse.Name,
            locationId, balances.FirstOrDefault(b => b.Location is not null)?.Location?.Name,
            batchNumber,
            onHand, reserved, onHand - reserved,
            // Weighted-average across the matched rows, so the caller sees the cost it would draw.
            onHand > 0 ? balances.Sum(b => b.QuantityOnHand * b.UnitCost) / onHand : 0m);
    }

    private Task<ScanResultDto?> TryResolveAsync(
        ScanKind kind,
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken) => kind switch
    {
        ScanKind.Product => TryProductAsync(payload, raw, cancellationToken),
        ScanKind.ProductVariant => TryVariantAsync(payload, raw, cancellationToken),
        ScanKind.Serial => TrySerialAsync(payload, raw, cancellationToken),
        ScanKind.Location => TryLocationAsync(payload, raw, cancellationToken),
        ScanKind.Warehouse => TryWarehouseAsync(payload, raw, cancellationToken),
        ScanKind.SalesOrder => TrySalesOrderAsync(payload, raw, cancellationToken),
        ScanKind.PurchaseOrder => TryPurchaseOrderAsync(payload, raw, cancellationToken),
        _ => Task.FromResult<ScanResultDto?>(null)
    };

    private async Task<ScanResultDto?> TryProductAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        var code = payload.Code;

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Barcode == code, cancellationToken)
            ?? await _context.Products.FirstOrDefaultAsync(p => p.Sku == code, cancellationToken);

        if (product is null) return null;

        return new ScanResultDto(
            nameof(ScanKind.Product), true, raw, ScanPayloadDto.From(payload),
            Product: await LoadProductAsync(product.Id, null, cancellationToken));
    }

    private async Task<ScanResultDto?> TryVariantAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        var code = payload.Code;

        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Barcode == code, cancellationToken)
            ?? await _context.ProductVariants.FirstOrDefaultAsync(v => v.Sku == code, cancellationToken);

        if (variant is null) return null;

        return new ScanResultDto(
            nameof(ScanKind.ProductVariant), true, raw, ScanPayloadDto.From(payload),
            Product: await LoadProductAsync(variant.ProductId, variant, cancellationToken));
    }

    private async Task<ScanResultDto?> TrySerialAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        // The serial may be the whole label, or a field within a GS1/JSON payload.
        var candidates = new[] { payload.SerialNumber, payload.Code }
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        foreach (var candidate in candidates)
        {
            var serial = await _context.ProductSerials
                .Include(s => s.Product)
                .Include(s => s.Warehouse)
                .Include(s => s.Location)
                .FirstOrDefaultAsync(s => s.SerialNumber == candidate, cancellationToken);

            if (serial is null) continue;

            return new ScanResultDto(
                nameof(ScanKind.Serial), true, raw, ScanPayloadDto.From(payload),
                Serial: ToDto(serial));
        }

        return null;
    }

    private async Task<ScanResultDto?> TryLocationAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        var code = payload.Code;

        var location = await _context.WarehouseLocations
            .Include(l => l.Warehouse)
            .FirstOrDefaultAsync(l => l.Barcode == code, cancellationToken)
            ?? await _context.WarehouseLocations
                .Include(l => l.Warehouse)
                .FirstOrDefaultAsync(l => l.Code == code, cancellationToken);

        if (location is null) return null;

        return new ScanResultDto(
            nameof(ScanKind.Location), true, raw, ScanPayloadDto.From(payload),
            Location: new ScannedLocationDto(
                location.Id, location.Name, location.Code, location.Barcode,
                location.WarehouseId, location.Warehouse.Name,
                location.Aisle, location.Rack, location.Bin, location.IsActive));
    }

    private async Task<ScanResultDto?> TryWarehouseAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        var code = payload.Code;

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Code == code, cancellationToken);

        if (warehouse is null) return null;

        return new ScanResultDto(
            nameof(ScanKind.Warehouse), true, raw, ScanPayloadDto.From(payload),
            Warehouse: new ScannedWarehouseDto(warehouse.Id, warehouse.Name, warehouse.Code, warehouse.IsActive));
    }

    private async Task<ScanResultDto?> TrySalesOrderAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        var code = payload.Code;

        var order = await _context.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == code, cancellationToken);

        if (order is null) return null;

        return new ScanResultDto(
            nameof(ScanKind.SalesOrder), true, raw, ScanPayloadDto.From(payload),
            Document: new ScannedDocumentDto(
                order.Id, "SalesOrder", order.OrderNumber, order.Status.ToString(),
                order.WarehouseId, order.Warehouse.Name, order.Customer.Name,
                order.OrderDate, order.Items.Count));
    }

    private async Task<ScanResultDto?> TryPurchaseOrderAsync(
        BarcodePayload payload,
        string raw,
        CancellationToken cancellationToken)
    {
        var code = payload.Code;

        var order = await _context.PurchaseOrders
            .Include(o => o.Supplier)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == code, cancellationToken);

        if (order is null) return null;

        return new ScanResultDto(
            nameof(ScanKind.PurchaseOrder), true, raw, ScanPayloadDto.From(payload),
            Document: new ScannedDocumentDto(
                order.Id, "PurchaseOrder", order.OrderNumber, order.Status.ToString(),
                order.WarehouseId, order.Warehouse.Name, order.Supplier.Name,
                order.OrderDate, order.Items.Count));
    }

    /// <summary>
    /// Loads a product together with the stock behind it: every balance row with its warehouse,
    /// bin, batch and expiry, plus the serials currently on hand.
    /// </summary>
    private async Task<ScannedProductDto> LoadProductAsync(
        Guid productId,
        ProductVariant? variant,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.UnitOfMeasure)
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), productId);

        var balances = await _context.InventoryBalances
            .Include(b => b.Warehouse)
            .Include(b => b.Location)
            .Where(b => b.ProductId == productId)
            .OrderBy(b => b.Warehouse.Name)
                .ThenBy(b => b.ExpiryDate)
            .ToListAsync(cancellationToken);

        var stock = balances
            .Select(b => new ScannedStockDto(
                b.Id, b.WarehouseId, b.Warehouse.Name,
                b.LocationId, b.Location?.Name, b.Location?.Code,
                b.BatchNumber, b.ExpiryDate,
                b.QuantityOnHand, b.QuantityReserved, b.QuantityOnHand - b.QuantityReserved,
                b.UnitCost))
            .ToList();

        // Only worth querying for products that actually track serials.
        var serials = product.TrackSerial
            ? await _context.ProductSerials
                .Include(s => s.Product)
                .Include(s => s.Warehouse)
                .Include(s => s.Location)
                .Where(s => s.ProductId == productId &&
                            (s.Status == SerialStatus.InStock || s.Status == SerialStatus.Reserved))
                .OrderBy(s => s.SerialNumber)
                .Take(MaxSerialsReturned)
                .ToListAsync(cancellationToken)
            : [];

        return new ScannedProductDto(
            product.Id, product.Name, product.Sku, product.Barcode,
            product.Category.Name, product.Brand?.Name, product.UnitOfMeasure.Name,
            product.CostPrice, product.SellingPrice, product.ReorderLevel,
            product.TrackExpiry, product.TrackBatch, product.TrackSerial, product.IsActive,
            variant?.Id, variant?.Name,
            stock.Sum(s => s.QuantityOnHand),
            stock.Sum(s => s.QuantityAvailable),
            stock,
            serials.Select(ToDto).ToList());
    }

    private static ScannedSerialDto ToDto(ProductSerial serial) => new(
        serial.Id, serial.ProductId, serial.Product.Name, serial.Product.Sku,
        serial.SerialNumber, serial.Status.ToString(),
        serial.WarehouseId, serial.Warehouse?.Name,
        serial.LocationId, serial.Location?.Name,
        serial.BatchNumber, serial.ExpiryDate);

    private static ScanResultDto Unmatched(string raw, BarcodePayload payload, string message) =>
        new(nameof(ScanKind.Unknown), false, raw, ScanPayloadDto.From(payload), message);

    /// <summary>Unrecognised kind names are ignored rather than rejected, so a newer client
    /// asking for a kind this build doesn't know still gets the kinds it does.</summary>
    private static HashSet<ScanKind>? ParseExpectedKinds(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count == 0) return null;

        var kinds = names
            .Select(n => Enum.TryParse<ScanKind>(n, ignoreCase: true, out var kind) ? kind : (ScanKind?)null)
            .Where(k => k is not null and not ScanKind.Unknown)
            .Select(k => k!.Value)
            .ToHashSet();

        return kinds.Count > 0 ? kinds : null;
    }
}
