using System.Text.Json;
using InventorySaaS.Application.Common.Barcodes;
using InventorySaaS.Application.Features.Picking.DTOs;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

/// <summary>
/// Sales-order picking.
///
/// Picking records intent, not movement: quantities accumulate on
/// <see cref="SalesOrderItem.PickedQuantity"/> and stock only moves when the session is
/// completed with delivery, which calls the existing <see cref="ISalesOrderService"/> path
/// rather than reimplementing the draw-down, reservation release and COGS logic.
///
/// Because picking never changes <see cref="SalesOrderStatus"/>, no stored enum value shifts
/// meaning and the existing order screens keep working untouched.
/// </summary>
public class PickingService : IPickingService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly ISalesOrderService _salesOrderService;

    public PickingService(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        ISalesOrderService salesOrderService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _salesOrderService = salesOrderService;
    }

    public async Task<PickSessionDto?> GetAsync(Guid salesOrderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(salesOrderId, cancellationToken);
        var session = await LoadOpenSessionAsync(salesOrderId, cancellationToken);

        return session is null ? null : ToDto(session, order);
    }

    public async Task<PickSessionDto> StartAsync(
        Guid salesOrderId,
        StartPickRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(salesOrderId, cancellationToken);

        if (order.Status is not (SalesOrderStatus.Confirmed or SalesOrderStatus.PartiallyDelivered))
        {
            throw new BadRequestException(
                $"Cannot pick a sales order with status '{order.Status}'. Confirm the order first.");
        }

        // Re-entering the screen must not spawn a second run against the same order.
        var existing = await LoadOpenSessionAsync(salesOrderId, cancellationToken);
        if (existing is not null) return ToDto(existing, order);

        var session = new SalesOrderPickSession
        {
            TenantId = _currentUserService.TenantId!.Value,
            SalesOrderId = salesOrderId,
            Status = PickSessionStatus.Open,
            StartedAt = DateTime.UtcNow,
            PickedBy = _currentUserService.Email,
            Notes = Normalise(request.Notes),
        };

        _context.SalesOrderPickSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(session, order);
    }

    public async Task<PickScanResultDto> ScanAsync(
        Guid salesOrderId,
        PickScanRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null)
    {
        var endpoint = $"sales-orders/{salesOrderId}/picking/scan";

        var replay = await TryReplayAsync(idempotencyKey, endpoint, cancellationToken);
        if (replay is not null) return replay;

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        var order = await LoadOrderAsync(salesOrderId, cancellationToken);
        var session = await LoadOpenSessionAsync(salesOrderId, cancellationToken)
            ?? throw new BadRequestException("No picking session is open for this order. Start picking first.");

        var productId = await ResolveProductIdAsync(request, cancellationToken);

        var item = order.Items.FirstOrDefault(i => i.ProductId == productId)
            ?? throw new BadRequestException(
                "That product is not on this order. Check the item and scan again.");

        // Delivered units are already gone, so only the undelivered remainder can be picked.
        var pickable = item.Quantity - item.DeliveredQuantity;
        var remaining = pickable - item.PickedQuantity;

        if (remaining <= 0)
        {
            throw new BadRequestException(
                $"'{item.Product.Name}' is already fully picked ({item.PickedQuantity} of {pickable}).");
        }

        if (request.Quantity > remaining)
        {
            throw new BadRequestException(
                $"Only {remaining} more of '{item.Product.Name}' can be picked, not {request.Quantity}.");
        }

        item.PickedQuantity += request.Quantity;

        _context.SalesOrderPickEvents.Add(new SalesOrderPickEvent
        {
            TenantId = _currentUserService.TenantId!.Value,
            SessionId = session.Id,
            ProductId = productId,
            Quantity = request.Quantity,
            BatchNumber = Normalise(request.BatchNumber),
            SerialNumber = Normalise(request.SerialNumber),
            ScannedAt = DateTime.UtcNow,
        });

        var sessionDto = ToDto(session, order);
        var line = sessionDto.Lines.First(l => l.ProductId == productId);

        var result = new PickScanResultDto(
            true,
            line.RemainingQuantity == 0
                ? $"{item.Product.Name} complete ({line.PickedQuantity} of {line.RequestedQuantity})"
                : $"{item.Product.Name}: {line.PickedQuantity} of {line.RequestedQuantity}, {line.RemainingQuantity} to go",
            line,
            sessionDto);

        return await SaveWithIdempotencyAsync(result, idempotencyKey, endpoint, cancellationToken);
    }

    public async Task<PickSessionDto> CompleteAsync(
        Guid salesOrderId,
        CompletePickRequest request,
        CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(salesOrderId, cancellationToken);
        var session = await LoadOpenSessionAsync(salesOrderId, cancellationToken)
            ?? throw new BadRequestException("No picking session is open for this order.");

        var outstanding = order.Items
            .Where(i => i.PickedQuantity < i.Quantity - i.DeliveredQuantity)
            .Select(i => $"{i.Product.Name} ({i.PickedQuantity}/{i.Quantity - i.DeliveredQuantity})")
            .ToList();

        if (outstanding.Count > 0)
        {
            throw new BadRequestException(
                $"Picking is not finished. Still outstanding: {string.Join(", ", outstanding)}.");
        }

        session.Status = PickSessionStatus.Completed;
        session.CompletedAt = DateTime.UtcNow;
        if (Normalise(request.Notes) is { } notes) session.Notes = notes;

        await _context.SaveChangesAsync(cancellationToken);

        if (request.Deliver)
        {
            // Reuse the existing delivery path: it releases reservations, draws stock
            // earliest-expiry-first, computes COGS and advances the order status.
            var items = order.Items
                .Where(i => i.PickedQuantity > 0)
                .Select(i => new DeliverSalesOrderItemRequest(i.ProductId, i.PickedQuantity))
                .ToList();

            if (items.Count > 0)
            {
                await _salesOrderService.DeliverAsync(
                    salesOrderId,
                    new DeliverSalesOrderRequest(salesOrderId, items, $"Picked via scanner ({session.Id})"),
                    cancellationToken);
            }

            // Delivery has consumed the picked quantities, so the counters reset for any
            // remaining balance on a partially delivered order.
            var delivered = await LoadOrderAsync(salesOrderId, cancellationToken);
            foreach (var item in delivered.Items) item.PickedQuantity = 0;
            await _context.SaveChangesAsync(cancellationToken);

            return ToDto(session, delivered);
        }

        return ToDto(session, order);
    }

    public async Task<PickSessionDto> CancelAsync(Guid salesOrderId, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(salesOrderId, cancellationToken);
        var session = await LoadOpenSessionAsync(salesOrderId, cancellationToken)
            ?? throw new BadRequestException("No picking session is open for this order.");

        session.Status = PickSessionStatus.Cancelled;
        session.CompletedAt = DateTime.UtcNow;

        // Abandoning a run must not leave phantom picked quantities behind on the order.
        foreach (var item in order.Items) item.PickedQuantity = 0;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(session, order);
    }

    // ---------------------------------------------------------------------------------------

    private async Task<SalesOrder> LoadOrderAsync(Guid salesOrderId, CancellationToken cancellationToken) =>
        await _context.SalesOrders
            .Include(o => o.Customer)
            .Include(o => o.Warehouse)
            .Include(o => o.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(o => o.Id == salesOrderId, cancellationToken)
        ?? throw new NotFoundException(nameof(SalesOrder), salesOrderId);

    private Task<SalesOrderPickSession?> LoadOpenSessionAsync(
        Guid salesOrderId,
        CancellationToken cancellationToken) =>
        _context.SalesOrderPickSessions
            .FirstOrDefaultAsync(
                s => s.SalesOrderId == salesOrderId && s.Status == PickSessionStatus.Open,
                cancellationToken);

    /// <summary>
    /// Works out which product was scanned. A barcode is decoded and matched against product
    /// and variant barcodes, SKUs and serial numbers, so any label on the item identifies it.
    /// </summary>
    private async Task<Guid> ResolveProductIdAsync(
        PickScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId.HasValue) return request.ProductId.Value;

        if (string.IsNullOrWhiteSpace(request.Barcode))
            throw new BadRequestException("Either a product or a scanned barcode is required.");

        var payload = BarcodePayloadParser.Parse(request.Barcode);
        var code = payload.Code;

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == code, cancellationToken);
        if (product is not null) return product.Id;

        product = await _context.Products.FirstOrDefaultAsync(p => p.Sku == code, cancellationToken);
        if (product is not null) return product.Id;

        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Barcode == code || v.Sku == code, cancellationToken);
        if (variant is not null) return variant.ProductId;

        // The label may name a serial rather than the product itself.
        var serialCandidate = payload.SerialNumber ?? code;
        var serial = await _context.ProductSerials
            .FirstOrDefaultAsync(s => s.SerialNumber == serialCandidate, cancellationToken);
        if (serial is not null) return serial.ProductId;

        throw new BadRequestException($"'{code}' is not assigned to a product.");
    }

    private static PickSessionDto ToDto(SalesOrderPickSession session, SalesOrder order)
    {
        var lines = order.Items
            .OrderBy(i => i.Product.Name)
            .Select(i =>
            {
                var pickable = i.Quantity - i.DeliveredQuantity;
                return new PickLineDto(
                    i.Id, i.ProductId, i.Product.Name, i.Product.Sku, i.Product.Barcode,
                    i.Product.TrackBatch, i.Product.TrackSerial,
                    pickable, i.PickedQuantity, Math.Max(0, pickable - i.PickedQuantity),
                    i.DeliveredQuantity);
            })
            .ToList();

        var requested = lines.Sum(l => l.RequestedQuantity);
        var picked = lines.Sum(l => l.PickedQuantity);

        return new PickSessionDto(
            session.Id, order.Id, order.OrderNumber, order.Status.ToString(),
            order.Customer.Name, order.WarehouseId, order.Warehouse.Name,
            session.Status.ToString(), session.StartedAt, session.CompletedAt,
            session.PickedBy, session.Notes,
            requested, picked, Math.Max(0, requested - picked),
            requested > 0 && picked >= requested,
            lines);
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // --- idempotency (mirrors InventoryService, keyed per order) ----------------------------

    private async Task<PickScanResultDto?> TryReplayAsync(
        string? idempotencyKey,
        string endpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var key = idempotencyKey.Trim();
        var existing = await _context.ScanIdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == key, cancellationToken);

        if (existing is null) return null;

        if (!string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
            throw new ConflictException("That idempotency key was already used for a different operation.");

        return existing.ResponseJson is null
            ? throw new ConflictException("An identical request is still being processed.")
            : JsonSerializer.Deserialize<PickScanResultDto>(existing.ResponseJson);
    }

    private async Task<PickScanResultDto> SaveWithIdempotencyAsync(
        PickScanResultDto result,
        string? idempotencyKey,
        string endpoint,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _context.ScanIdempotencyKeys.Add(new ScanIdempotencyKey
            {
                TenantId = _currentUserService.TenantId!.Value,
                Key = idempotencyKey.Trim(),
                Endpoint = endpoint,
                ResponseJson = JsonSerializer.Serialize(result),
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var replay = await TryReplayAsync(idempotencyKey, endpoint, cancellationToken);
            if (replay is not null) return replay;
            throw;
        }

        return result;
    }
}
