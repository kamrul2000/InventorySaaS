using System.Text.Json;
using InventorySaaS.Application.Common.Barcodes;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.StockCounts.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

/// <summary>
/// Physical stock counting.
///
/// Counting is deliberately inert: scanning only accumulates counted quantities against a
/// snapshot of what the system believed when each line was opened. Nothing reaches inventory
/// until a Manager approves, at which point each varying line produces one
/// <see cref="TransactionType.Adjustment"/> transaction — so every correction keeps the audit
/// trail the rest of the system already relies on.
/// </summary>
public class StockCountService : IStockCountService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StockCountService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<StockCountSessionDto>> GetAllAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _context.StockCountSessions
            .Include(s => s.Warehouse)
            .Include(s => s.Location)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Location)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Serials)
            .AsQueryable();

        if (warehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == warehouseId.Value);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<StockCountStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(s => s.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var term = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(s => s.CountNumber.ToLower().Contains(term));
        }

        query = query.OrderByDescending(s => s.StartedAt);

        // Projected in memory because the DTO folds the line collection into summary counts.
        var page = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var total = await query.CountAsync(cancellationToken);

        return new PaginatedList<StockCountSessionDto>(
            page.Select(ToDto).ToList(), total, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<StockCountSessionDto> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        ToDto(await LoadSessionAsync(id, cancellationToken));

    public async Task<StockCountSessionDto> StartAsync(
        StartStockCountRequest request,
        CancellationToken cancellationToken)
    {
        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        if (request.LocationId.HasValue)
        {
            var belongs = await _context.WarehouseLocations.AnyAsync(
                l => l.Id == request.LocationId.Value && l.WarehouseId == request.WarehouseId,
                cancellationToken);

            if (!belongs)
                throw new BadRequestException("The selected location does not belong to that warehouse.");
        }

        // Two open counts over the same scope would each snapshot the other's corrections.
        var conflicting = await _context.StockCountSessions.AnyAsync(
            s => s.WarehouseId == request.WarehouseId &&
                 s.LocationId == request.LocationId &&
                 (s.Status == StockCountStatus.Counting || s.Status == StockCountStatus.PendingApproval),
            cancellationToken);

        if (conflicting)
        {
            throw new ConflictException(
                "A stock count is already in progress for that warehouse and location.");
        }

        var session = new StockCountSession
        {
            TenantId = _currentUserService.TenantId!.Value,
            CountNumber = $"CNT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}",
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            Status = StockCountStatus.Counting,
            StartedAt = DateTime.UtcNow,
            Notes = Normalise(request.Notes),
        };

        _context.StockCountSessions.Add(session);
        await _context.SaveChangesAsync(cancellationToken);

        session.Warehouse = warehouse;
        return ToDto(session);
    }

    public async Task<StockCountScanResultDto> ScanAsync(
        Guid id,
        StockCountScanRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null)
    {
        var endpoint = $"stock-counts/{id}/scan";

        var replay = await TryReplayAsync(idempotencyKey, endpoint, cancellationToken);
        if (replay is not null) return replay;

        if (request.Quantity < 0)
            throw new BadRequestException("Quantity cannot be negative.");

        var session = await LoadSessionAsync(id, cancellationToken);

        if (session.Status != StockCountStatus.Counting)
            throw new BadRequestException($"This count is {session.Status} and can no longer be changed.");

        var productId = await ResolveProductIdAsync(request, cancellationToken);
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException("Product", productId);

        // A count narrowed to one bin pins every line to it; otherwise the scan says where.
        var locationId = session.LocationId ?? request.LocationId;

        if (locationId.HasValue)
        {
            var belongs = await _context.WarehouseLocations.AnyAsync(
                l => l.Id == locationId.Value && l.WarehouseId == session.WarehouseId, cancellationToken);

            if (!belongs)
                throw new BadRequestException("That location does not belong to the warehouse being counted.");
        }

        var batchNumber = Normalise(request.BatchNumber);

        var line = session.Lines.FirstOrDefault(l =>
            l.ProductId == productId &&
            l.LocationId == locationId &&
            l.BatchNumber == batchNumber);

        if (line is null)
        {
            // Snapshot what the system believes right now — the variance is measured against
            // the moment counting of this line began, not against approval time.
            var systemQuantity = await CurrentSystemQuantityAsync(
                session.WarehouseId, productId, locationId, batchNumber, cancellationToken);

            line = new StockCountLine
            {
                TenantId = _currentUserService.TenantId!.Value,
                SessionId = session.Id,
                ProductId = productId,
                LocationId = locationId,
                BatchNumber = batchNumber,
                SystemQuantity = systemQuantity,
                CountedQuantity = 0,
                Notes = Normalise(request.Notes),
            };

            session.Lines.Add(line);
            _context.StockCountLines.Add(line);
            line.Product = product;
        }
        else if (Normalise(request.Notes) is { } notes)
        {
            line.Notes = notes;
        }

        var serialNumber = Normalise(request.SerialNumber);

        if (serialNumber is not null)
        {
            // A serial is one physical unit, so it can only be counted once per line.
            if (line.Serials.Any(s => string.Equals(s.SerialNumber, serialNumber, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BadRequestException($"Serial {serialNumber} has already been counted on this line.");
            }

            var serialRow = new StockCountLineSerial
            {
                TenantId = _currentUserService.TenantId!.Value,
                LineId = line.Id,
                SerialNumber = serialNumber,
            };

            line.Serials.Add(serialRow);
            _context.StockCountLineSerials.Add(serialRow);
            line.CountedQuantity = line.Serials.Count;
        }
        else if (request.SetExactQuantity)
        {
            line.CountedQuantity = request.Quantity;
        }
        else
        {
            // Repeat scans add up — the normal case for counting a shelf item by item.
            line.CountedQuantity += request.Quantity;
        }

        var sessionDto = ToDto(session);
        var lineDto = sessionDto.Lines.First(l => l.Id == line.Id);

        var result = new StockCountScanResultDto(
            true,
            DescribeVariance(product.Name, lineDto),
            lineDto,
            sessionDto);

        return await SaveWithIdempotencyAsync(result, idempotencyKey, endpoint, cancellationToken);
    }

    public async Task<StockCountSessionDto> SubmitAsync(
        Guid id,
        SubmitStockCountRequest request,
        CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(id, cancellationToken);

        if (session.Status != StockCountStatus.Counting)
            throw new BadRequestException($"Only a count that is still being counted can be submitted (this one is {session.Status}).");

        if (session.Lines.Count == 0)
            throw new BadRequestException("Nothing has been counted yet.");

        session.Status = StockCountStatus.PendingApproval;
        session.SubmittedAt = DateTime.UtcNow;
        if (Normalise(request.Notes) is { } notes) session.Notes = notes;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(session);
    }

    public async Task<StockCountSessionDto> ApproveAsync(
        Guid id,
        ApproveStockCountRequest request,
        CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(id, cancellationToken);

        if (session.Status != StockCountStatus.PendingApproval)
        {
            throw new BadRequestException(
                $"Only a count awaiting approval can be approved (this one is {session.Status}).");
        }

        var tenantId = _currentUserService.TenantId!.Value;

        foreach (var line in session.Lines)
        {
            // Re-read the live quantity: stock may have moved between submission and approval,
            // and the adjustment must land the balance on the counted figure regardless.
            var balance = await FindBalanceAsync(
                session.WarehouseId, line.ProductId, line.LocationId, line.BatchNumber, cancellationToken);

            var liveQuantity = balance?.QuantityOnHand ?? 0;
            var delta = line.CountedQuantity - liveQuantity;

            if (delta == 0) continue;

            if (balance is null)
            {
                var product = await _context.Products.FirstAsync(p => p.Id == line.ProductId, cancellationToken);

                balance = new InventoryBalance
                {
                    TenantId = tenantId,
                    ProductId = line.ProductId,
                    WarehouseId = session.WarehouseId,
                    LocationId = line.LocationId,
                    BatchNumber = line.BatchNumber,
                    QuantityOnHand = 0,
                    UnitCost = product.CostPrice,
                };
                _context.InventoryBalances.Add(balance);
            }

            balance.QuantityOnHand = line.CountedQuantity;

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                TenantId = tenantId,
                TransactionNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}",
                TransactionType = TransactionType.Adjustment,
                ProductId = line.ProductId,
                WarehouseId = session.WarehouseId,
                LocationId = line.LocationId,
                Quantity = delta,
                UnitCost = balance.UnitCost,
                BatchNumber = line.BatchNumber,
                Reason = "Stock count",
                ReferenceType = "StockCount",
                ReferenceNumber = session.CountNumber,
                ReferenceId = session.Id,
                Notes = $"Count {session.CountNumber}: system {liveQuantity} -> counted {line.CountedQuantity}.",
                TransactionDate = DateTime.UtcNow,
            });
        }

        session.Status = StockCountStatus.Approved;
        session.ApprovedAt = DateTime.UtcNow;
        session.ApprovedBy = _currentUserService.Email;
        if (Normalise(request.Notes) is { } notes) session.Notes = notes;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(session);
    }

    public async Task<StockCountSessionDto> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var session = await LoadSessionAsync(id, cancellationToken);

        if (session.Status == StockCountStatus.Approved)
            throw new BadRequestException("An approved count cannot be cancelled — its adjustments are already posted.");

        session.Status = StockCountStatus.Cancelled;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(session);
    }

    // ---------------------------------------------------------------------------------------

    private async Task<StockCountSession> LoadSessionAsync(Guid id, CancellationToken cancellationToken) =>
        await _context.StockCountSessions
            .Include(s => s.Warehouse)
            .Include(s => s.Location)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Product)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Location)
            .Include(s => s.Lines)
                .ThenInclude(l => l.Serials)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new NotFoundException(nameof(StockCountSession), id);

    private Task<InventoryBalance?> FindBalanceAsync(
        Guid warehouseId,
        Guid productId,
        Guid? locationId,
        string? batchNumber,
        CancellationToken cancellationToken) =>
        _context.InventoryBalances.FirstOrDefaultAsync(
            b => b.ProductId == productId &&
                 b.WarehouseId == warehouseId &&
                 b.LocationId == locationId &&
                 b.BatchNumber == batchNumber,
            cancellationToken);

    /// <summary>
    /// What the system currently holds for this product, bin and batch. A count with no bin
    /// narrows to the bin-less row, matching how balances are keyed everywhere else.
    /// </summary>
    private async Task<int> CurrentSystemQuantityAsync(
        Guid warehouseId,
        Guid productId,
        Guid? locationId,
        string? batchNumber,
        CancellationToken cancellationToken)
    {
        var balance = await FindBalanceAsync(warehouseId, productId, locationId, batchNumber, cancellationToken);
        return balance?.QuantityOnHand ?? 0;
    }

    private async Task<Guid> ResolveProductIdAsync(
        StockCountScanRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ProductId.HasValue) return request.ProductId.Value;

        if (string.IsNullOrWhiteSpace(request.Barcode))
            throw new BadRequestException("Either a product or a scanned barcode is required.");

        var payload = BarcodePayloadParser.Parse(request.Barcode);
        var code = payload.Code;

        var product = await _context.Products.FirstOrDefaultAsync(p => p.Barcode == code, cancellationToken)
            ?? await _context.Products.FirstOrDefaultAsync(p => p.Sku == code, cancellationToken);
        if (product is not null) return product.Id;

        var variant = await _context.ProductVariants
            .FirstOrDefaultAsync(v => v.Barcode == code || v.Sku == code, cancellationToken);
        if (variant is not null) return variant.ProductId;

        var serialCandidate = payload.SerialNumber ?? code;
        var serial = await _context.ProductSerials
            .FirstOrDefaultAsync(s => s.SerialNumber == serialCandidate, cancellationToken);
        if (serial is not null) return serial.ProductId;

        throw new BadRequestException($"'{code}' is not assigned to a product.");
    }

    private static string DescribeVariance(string productName, StockCountLineDto line) => line.Variance switch
    {
        0 => $"{productName}: {line.CountedQuantity} counted — matches the system.",
        > 0 => $"{productName}: {line.CountedQuantity} counted — {line.Variance} more than the system.",
        _ => $"{productName}: {line.CountedQuantity} counted — {Math.Abs(line.Variance)} short.",
    };

    private static StockCountSessionDto ToDto(StockCountSession session)
    {
        var lines = session.Lines
            .OrderBy(l => l.Product?.Name)
            .Select(l => new StockCountLineDto(
                l.Id, l.ProductId,
                l.Product?.Name ?? string.Empty,
                l.Product?.Sku ?? string.Empty,
                l.Product?.Barcode,
                l.LocationId, l.Location?.Name, l.BatchNumber,
                l.Product?.TrackSerial ?? false,
                l.SystemQuantity, l.CountedQuantity, l.Variance, l.Notes,
                l.Serials.Select(s => s.SerialNumber).OrderBy(s => s).ToList()))
            .ToList();

        return new StockCountSessionDto(
            session.Id, session.CountNumber,
            session.WarehouseId, session.Warehouse?.Name ?? string.Empty,
            session.LocationId, session.Location?.Name,
            session.Status.ToString(),
            session.StartedAt, session.SubmittedAt, session.ApprovedAt, session.ApprovedBy, session.Notes,
            lines.Count,
            lines.Count(l => l.Variance < 0),
            lines.Count(l => l.Variance > 0),
            lines.Sum(l => l.Variance),
            lines);
    }

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // --- idempotency (mirrors InventoryService) ---------------------------------------------

    private async Task<StockCountScanResultDto?> TryReplayAsync(
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
            : JsonSerializer.Deserialize<StockCountScanResultDto>(existing.ResponseJson);
    }

    private async Task<StockCountScanResultDto> SaveWithIdempotencyAsync(
        StockCountScanResultDto result,
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
