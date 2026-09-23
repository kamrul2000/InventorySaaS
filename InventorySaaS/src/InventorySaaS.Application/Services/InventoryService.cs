using System.Text.Json;
using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class InventoryService : IInventoryService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public InventoryService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<InventoryBalanceDto>> GetBalancesAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        Guid? productId,
        CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Include(ib => ib.Location)
            .AsQueryable();

        if (warehouseId.HasValue) query = query.Where(ib => ib.WarehouseId == warehouseId.Value);
        if (productId.HasValue) query = query.Where(ib => ib.ProductId == productId.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "product" => pagination.SortDescending ? query.OrderByDescending(ib => ib.Product.Name) : query.OrderBy(ib => ib.Product.Name),
            "warehouse" => pagination.SortDescending ? query.OrderByDescending(ib => ib.Warehouse.Name) : query.OrderBy(ib => ib.Warehouse.Name),
            "quantity" => pagination.SortDescending ? query.OrderByDescending(ib => ib.QuantityOnHand) : query.OrderBy(ib => ib.QuantityOnHand),
            _ => query.OrderBy(ib => ib.Product.Name)
        };

        var projected = query.Select(ib => new InventoryBalanceDto(
            ib.Id, ib.ProductId, ib.Product.Name, ib.Product.Sku,
            ib.WarehouseId, ib.Warehouse.Name, ib.LocationId, ib.Location != null ? ib.Location.Name : null,
            ib.BatchNumber, ib.ExpiryDate,
            ib.QuantityOnHand, ib.QuantityReserved, ib.QuantityOnHand - ib.QuantityReserved,
            ib.UnitCost));

        return await PaginatedList<InventoryBalanceDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<PaginatedList<InventoryTransactionDto>> GetTransactionsAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        Guid? productId,
        CancellationToken cancellationToken)
    {
        var query = _context.InventoryTransactions
            .Include(t => t.Product)
            .Include(t => t.Warehouse)
            .AsQueryable();

        if (warehouseId.HasValue) query = query.Where(t => t.WarehouseId == warehouseId.Value);
        if (productId.HasValue) query = query.Where(t => t.ProductId == productId.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(t =>
                t.TransactionNumber.ToLower().Contains(searchTerm) ||
                t.Product.Name.ToLower().Contains(searchTerm) ||
                t.Product.Sku.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "date" => pagination.SortDescending ? query.OrderByDescending(t => t.TransactionDate) : query.OrderBy(t => t.TransactionDate),
            "product" => pagination.SortDescending ? query.OrderByDescending(t => t.Product.Name) : query.OrderBy(t => t.Product.Name),
            _ => query.OrderByDescending(t => t.TransactionDate)
        };

        var projected = query.Select(t => new InventoryTransactionDto(
            t.Id, t.TransactionNumber, t.TransactionType.ToString(),
            t.Product.Name, t.Product.Sku, t.Warehouse.Name,
            t.Quantity, t.UnitCost, t.BatchNumber, t.TransactionDate, t.Notes, t.Reason,
            t.SerialNumbers == null ? null : t.SerialNumbers.Split(',', StringSplitOptions.RemoveEmptyEntries)));

        return await PaginatedList<InventoryTransactionDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<InventoryTransactionDto> StockInAsync(
        StockInRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null)
    {
        const string Endpoint = "inventory/stock-in";

        var replay = await TryReplayAsync(idempotencyKey, Endpoint, cancellationToken);
        if (replay is not null) return replay;

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), request.ProductId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        await ValidateLocationAsync(request.LocationId, request.WarehouseId, cancellationToken);

        var batchNumber = Normalise(request.BatchNumber);
        if (product.TrackBatch && batchNumber is null)
            throw new BadRequestException($"'{product.Name}' is batch-tracked, so a batch number is required.");

        var serials = NormaliseSerials(request.SerialNumbers);
        ValidateSerialCount(product, serials, request.Quantity);

        var tenantId = _currentUserService.TenantId!.Value;

        if (serials.Count > 0)
            await GuardAgainstDuplicateSerialsAsync(serials, cancellationToken);

        var balance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.WarehouseId &&
                ib.LocationId == request.LocationId &&
                ib.BatchNumber == batchNumber,
                cancellationToken);

        if (balance is null)
        {
            balance = new InventoryBalance
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                BatchNumber = batchNumber,
                LotNumber = request.LotNumber,
                ExpiryDate = request.ExpiryDate,
                QuantityOnHand = 0,
                UnitCost = request.UnitCost
            };
            _context.InventoryBalances.Add(balance);
        }
        else if (request.ExpiryDate.HasValue)
        {
            // Receiving into an existing batch carries the expiry forward rather than losing it.
            balance.ExpiryDate ??= request.ExpiryDate;
        }

        // Recompute moving weighted-average cost rather than overwriting with the latest cost.
        balance.ApplyInbound(request.Quantity, request.UnitCost);

        var transaction = NewTransaction(tenantId, TransactionType.StockIn, request.ProductId, request.WarehouseId);
        transaction.LocationId = request.LocationId;
        transaction.Quantity = request.Quantity;
        transaction.UnitCost = request.UnitCost;
        transaction.BatchNumber = batchNumber;
        transaction.LotNumber = request.LotNumber;
        transaction.ExpiryDate = request.ExpiryDate;
        transaction.Notes = request.Notes;
        transaction.SerialNumbers = JoinSerials(serials);

        _context.InventoryTransactions.Add(transaction);

        foreach (var serialNumber in serials)
        {
            _context.ProductSerials.Add(new ProductSerial
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                SerialNumber = serialNumber,
                Status = SerialStatus.InStock,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                BatchNumber = batchNumber,
                ExpiryDate = request.ExpiryDate,
                UnitCost = request.UnitCost,
                LastTransactionId = transaction.Id
            });
        }

        var result = ToDto(transaction, product, warehouse, serials);
        return await SaveWithIdempotencyAsync(result, idempotencyKey, Endpoint, cancellationToken);
    }

    public async Task<InventoryTransactionDto> StockOutAsync(
        StockOutRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null)
    {
        const string Endpoint = "inventory/stock-out";

        var replay = await TryReplayAsync(idempotencyKey, Endpoint, cancellationToken);
        if (replay is not null) return replay;

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), request.ProductId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        await ValidateLocationAsync(request.LocationId, request.WarehouseId, cancellationToken);

        var batchNumber = Normalise(request.BatchNumber);
        if (product.TrackBatch && batchNumber is null)
            throw new BadRequestException($"'{product.Name}' is batch-tracked, so a batch number is required.");

        var serials = NormaliseSerials(request.SerialNumbers);
        ValidateSerialCount(product, serials, request.Quantity);

        var candidates = await LoadDrawableBalancesAsync(
            request.ProductId, request.WarehouseId, request.LocationId, batchNumber, cancellationToken);

        var available = candidates.Sum(b => b.QuantityOnHand - b.QuantityReserved);
        if (available < request.Quantity)
        {
            throw new BadRequestException(
                $"Insufficient stock for '{product.Name}'. Available: {available}, requested: {request.Quantity}.");
        }

        var serialRecords = serials.Count > 0
            ? await LoadIssuableSerialsAsync(request.ProductId, request.WarehouseId, serials, cancellationToken)
            : [];

        var (unitCost, drawnBatch) = DrawDown(candidates, request.Quantity);

        var transaction = NewTransaction(
            _currentUserService.TenantId!.Value, TransactionType.StockOut, request.ProductId, request.WarehouseId);
        transaction.LocationId = request.LocationId;
        transaction.Quantity = request.Quantity;
        transaction.UnitCost = unitCost;
        transaction.BatchNumber = batchNumber ?? drawnBatch;
        transaction.Notes = request.Notes;
        transaction.Reason = Normalise(request.Reason);
        transaction.SerialNumbers = JoinSerials(serials);

        _context.InventoryTransactions.Add(transaction);

        foreach (var serial in serialRecords)
        {
            serial.Status = SerialStatus.Issued;
            serial.LastTransactionId = transaction.Id;
        }

        var result = ToDto(transaction, product, warehouse, serials);
        return await SaveWithIdempotencyAsync(result, idempotencyKey, Endpoint, cancellationToken);
    }

    public async Task<InventoryTransactionDto> TransferAsync(
        StockTransferRequest request,
        CancellationToken cancellationToken,
        string? idempotencyKey = null)
    {
        const string Endpoint = "inventory/transfer";

        var replay = await TryReplayAsync(idempotencyKey, Endpoint, cancellationToken);
        if (replay is not null) return replay;

        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        if (request.SourceWarehouseId == request.DestinationWarehouseId &&
            request.SourceLocationId == request.DestinationLocationId)
            throw new BadRequestException("Source and destination must be different.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), request.ProductId);

        var sourceWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.SourceWarehouseId, cancellationToken)
            ?? throw new NotFoundException("SourceWarehouse", request.SourceWarehouseId);

        _ = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.DestinationWarehouseId, cancellationToken)
            ?? throw new NotFoundException("DestinationWarehouse", request.DestinationWarehouseId);

        await ValidateLocationAsync(request.SourceLocationId, request.SourceWarehouseId, cancellationToken);
        await ValidateLocationAsync(request.DestinationLocationId, request.DestinationWarehouseId, cancellationToken);

        var batchNumber = Normalise(request.BatchNumber);
        if (product.TrackBatch && batchNumber is null)
            throw new BadRequestException($"'{product.Name}' is batch-tracked, so a batch number is required.");

        var serials = NormaliseSerials(request.SerialNumbers);
        ValidateSerialCount(product, serials, request.Quantity);

        var candidates = await LoadDrawableBalancesAsync(
            request.ProductId, request.SourceWarehouseId, request.SourceLocationId, batchNumber, cancellationToken);

        var available = candidates.Sum(b => b.QuantityOnHand - b.QuantityReserved);
        if (available < request.Quantity)
        {
            throw new BadRequestException(
                $"Insufficient stock at source for '{product.Name}'. Available: {available}, requested: {request.Quantity}.");
        }

        var serialRecords = serials.Count > 0
            ? await LoadIssuableSerialsAsync(request.ProductId, request.SourceWarehouseId, serials, cancellationToken)
            : [];

        var tenantId = _currentUserService.TenantId!.Value;

        // Capture the batch and expiry before drawing down, so they survive onto the destination.
        var sourceExpiry = candidates.FirstOrDefault()?.ExpiryDate;
        var sourceLot = candidates.FirstOrDefault()?.LotNumber;

        var (unitCost, drawnBatch) = DrawDown(candidates, request.Quantity);
        var movedBatch = batchNumber ?? drawnBatch;

        var destBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.DestinationWarehouseId &&
                ib.LocationId == request.DestinationLocationId &&
                ib.BatchNumber == movedBatch,
                cancellationToken);

        if (destBalance is null)
        {
            destBalance = new InventoryBalance
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.DestinationWarehouseId,
                LocationId = request.DestinationLocationId,
                BatchNumber = movedBatch,
                LotNumber = sourceLot,
                ExpiryDate = sourceExpiry,
                QuantityOnHand = 0,
                UnitCost = unitCost
            };
            _context.InventoryBalances.Add(destBalance);
        }

        // Carry the source cost into the destination via weighted-average blend.
        destBalance.ApplyInbound(request.Quantity, unitCost);

        var transaction = NewTransaction(tenantId, TransactionType.Transfer, request.ProductId, request.SourceWarehouseId);
        transaction.LocationId = request.SourceLocationId;
        transaction.DestinationWarehouseId = request.DestinationWarehouseId;
        transaction.DestinationLocationId = request.DestinationLocationId;
        transaction.Quantity = request.Quantity;
        transaction.UnitCost = unitCost;
        transaction.BatchNumber = movedBatch;
        transaction.ExpiryDate = sourceExpiry;
        transaction.Notes = request.Notes;
        transaction.SerialNumbers = JoinSerials(serials);

        _context.InventoryTransactions.Add(transaction);

        // Serial-controlled units move with the stock rather than being reissued.
        foreach (var serial in serialRecords)
        {
            serial.WarehouseId = request.DestinationWarehouseId;
            serial.LocationId = request.DestinationLocationId;
            serial.LastTransactionId = transaction.Id;
        }

        var result = ToDto(transaction, product, sourceWarehouse, serials);
        return await SaveWithIdempotencyAsync(result, idempotencyKey, Endpoint, cancellationToken);
    }

    public async Task<InventoryTransactionDto> AdjustAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.NewQuantity < 0)
            throw new BadRequestException("Quantity cannot be negative.");

        // The non-nullable-reference-type check on the DTO only rejects null, not "" - an empty
        // reason defeats the point of requiring one for this audit-sensitive operation (INV-01).
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new BadRequestException("Reason is required.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), request.ProductId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        var tenantId = _currentUserService.TenantId!.Value;

        var balance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.WarehouseId &&
                ib.LocationId == request.LocationId,
                cancellationToken);

        var previousQuantity = balance?.QuantityOnHand ?? 0;
        var adjustmentQuantity = request.NewQuantity - previousQuantity;

        if (balance is null)
        {
            balance = new InventoryBalance
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                QuantityOnHand = request.NewQuantity,
                UnitCost = product.CostPrice
            };
            _context.InventoryBalances.Add(balance);
        }
        else
        {
            balance.QuantityOnHand = request.NewQuantity;
        }

        var transaction = NewTransaction(tenantId, TransactionType.Adjustment, request.ProductId, request.WarehouseId);
        transaction.LocationId = request.LocationId;
        transaction.Quantity = adjustmentQuantity;
        transaction.UnitCost = balance.UnitCost;
        transaction.BatchNumber = balance.BatchNumber;
        transaction.Reason = request.Reason;
        transaction.Notes = $"Adjustment: {previousQuantity} -> {request.NewQuantity}. Reason: {request.Reason}";

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(transaction, product, warehouse, []);
    }

    // ---------------------------------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// The balance rows a movement may draw from, earliest-expiry-first.
    ///
    /// When a batch is named, only that batch qualifies — which is what keeps a batch-tracked
    /// product's batch attached to its movement. With no batch named, the draw falls back to
    /// FEFO across every batch in the bin, matching how sales-order delivery already behaves.
    /// </summary>
    private async Task<List<InventoryBalance>> LoadDrawableBalancesAsync(
        Guid productId,
        Guid warehouseId,
        Guid? locationId,
        string? batchNumber,
        CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .Where(ib => ib.ProductId == productId && ib.WarehouseId == warehouseId);

        // A null location means "this exact bin-less row", not "any bin", matching how
        // stock is received: LocationId is part of the balance's identity.
        query = query.Where(ib => ib.LocationId == locationId);

        if (batchNumber is not null)
            query = query.Where(ib => ib.BatchNumber == batchNumber);

        return await query
            .OrderBy(ib => ib.ExpiryDate == null)
            .ThenBy(ib => ib.ExpiryDate)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Removes <paramref name="quantity"/> across the candidate rows in order, and reports the
    /// weighted-average cost of what was actually drawn plus the batch, when a single batch
    /// covered the whole movement.
    /// </summary>
    private static (decimal UnitCost, string? BatchNumber) DrawDown(
        List<InventoryBalance> candidates,
        int quantity)
    {
        var remaining = quantity;
        decimal totalCost = 0;
        var batchesTouched = new HashSet<string>(StringComparer.Ordinal);
        var sawNullBatch = false;

        foreach (var balance in candidates)
        {
            if (remaining <= 0) break;

            var drawable = balance.QuantityOnHand - balance.QuantityReserved;
            if (drawable <= 0) continue;

            var toDeduct = Math.Min(drawable, remaining);
            totalCost += toDeduct * balance.UnitCost;
            balance.QuantityOnHand -= toDeduct;
            remaining -= toDeduct;

            if (balance.BatchNumber is null) sawNullBatch = true;
            else batchesTouched.Add(balance.BatchNumber);
        }

        var unitCost = quantity > 0 ? totalCost / quantity : 0m;

        // Only claim a batch when the movement came wholly from one; otherwise the transaction
        // would misreport which batch left the building.
        var batch = !sawNullBatch && batchesTouched.Count == 1 ? batchesTouched.First() : null;

        return (unitCost, batch);
    }

    /// <summary>
    /// Fetches the named serials and checks each is in stock at the source warehouse, so a
    /// mis-scanned or already-issued unit is rejected before anything moves.
    /// </summary>
    private async Task<List<ProductSerial>> LoadIssuableSerialsAsync(
        Guid productId,
        Guid warehouseId,
        IReadOnlyList<string> serialNumbers,
        CancellationToken cancellationToken)
    {
        var records = await _context.ProductSerials
            .Where(s => s.ProductId == productId && serialNumbers.Contains(s.SerialNumber))
            .ToListAsync(cancellationToken);

        var found = records.Select(s => s.SerialNumber).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = serialNumbers.Where(s => !found.Contains(s)).ToList();
        if (missing.Count > 0)
            throw new BadRequestException($"Unknown serial number(s): {string.Join(", ", missing)}.");

        var notInStock = records.Where(s => s.Status != SerialStatus.InStock).ToList();
        if (notInStock.Count > 0)
        {
            throw new BadRequestException(
                $"Serial number(s) not in stock: {string.Join(", ", notInStock.Select(s => $"{s.SerialNumber} ({s.Status})"))}.");
        }

        var elsewhere = records.Where(s => s.WarehouseId != warehouseId).ToList();
        if (elsewhere.Count > 0)
        {
            throw new BadRequestException(
                $"Serial number(s) are held in a different warehouse: {string.Join(", ", elsewhere.Select(s => s.SerialNumber))}.");
        }

        return records;
    }

    /// <summary>
    /// Rejects serials that already exist for this tenant. The filtered unique index is the
    /// real guarantee; this exists to produce a readable message instead of a constraint error.
    /// </summary>
    private async Task GuardAgainstDuplicateSerialsAsync(
        IReadOnlyList<string> serialNumbers,
        CancellationToken cancellationToken)
    {
        var existing = await _context.ProductSerials
            .Where(s => serialNumbers.Contains(s.SerialNumber))
            .Select(s => s.SerialNumber)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
            throw new ConflictException($"Serial number(s) already exist: {string.Join(", ", existing)}.");
    }

    /// <summary>A bin must belong to the warehouse it is being used with.</summary>
    private async Task ValidateLocationAsync(
        Guid? locationId,
        Guid warehouseId,
        CancellationToken cancellationToken)
    {
        if (!locationId.HasValue) return;

        var belongs = await _context.WarehouseLocations
            .AnyAsync(l => l.Id == locationId.Value && l.WarehouseId == warehouseId, cancellationToken);

        if (!belongs)
            throw new BadRequestException("The selected location does not belong to that warehouse.");
    }

    private static void ValidateSerialCount(ProductInfo product, IReadOnlyList<string> serials, int quantity)
    {
        if (product.TrackSerial)
        {
            if (serials.Count == 0)
                throw new BadRequestException($"'{product.Name}' is serial-tracked, so serial numbers are required.");

            if (serials.Count != quantity)
            {
                throw new BadRequestException(
                    $"Quantity is {quantity} but {serials.Count} serial number(s) were supplied — they must match.");
            }
        }
        else if (serials.Count > 0)
        {
            throw new BadRequestException($"'{product.Name}' is not serial-tracked, so serial numbers cannot be recorded.");
        }
    }

    private InventoryTransaction NewTransaction(
        Guid tenantId,
        TransactionType type,
        Guid productId,
        Guid warehouseId) => new()
    {
        TenantId = tenantId,
        TransactionNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}",
        TransactionType = type,
        ProductId = productId,
        WarehouseId = warehouseId,
        TransactionDate = DateTime.UtcNow
    };

    private static InventoryTransactionDto ToDto(
        InventoryTransaction transaction,
        ProductInfo product,
        WarehouseInfo warehouse,
        IReadOnlyList<string> serials) => new(
        transaction.Id, transaction.TransactionNumber, transaction.TransactionType.ToString(),
        product.Name, product.Sku, warehouse.Name,
        transaction.Quantity, transaction.UnitCost, transaction.BatchNumber,
        transaction.TransactionDate, transaction.Notes, transaction.Reason,
        serials.Count > 0 ? serials : null);

    private static string? Normalise(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> NormaliseSerials(IReadOnlyList<string>? serials)
    {
        if (serials is null) return [];

        var cleaned = serials
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .ToList();

        // A double-scan of the same unit within one request is an operator error, not a quantity.
        var duplicates = cleaned
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicates.Count > 0)
            throw new BadRequestException($"The same serial number was scanned more than once: {string.Join(", ", duplicates)}.");

        return cleaned;
    }

    private static string? JoinSerials(IReadOnlyList<string> serials) =>
        serials.Count > 0 ? string.Join(',', serials) : null;

    // ---------------------------------------------------------------------------------------
    // Idempotency
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the original response when this key has already been used, so a double-scan or a
    /// client retry does not post a second transaction.
    /// </summary>
    private async Task<InventoryTransactionDto?> TryReplayAsync(
        string? idempotencyKey,
        string endpoint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey)) return null;

        var key = idempotencyKey.Trim();
        var existing = await _context.ScanIdempotencyKeys
            .FirstOrDefaultAsync(k => k.Key == key, cancellationToken);

        if (existing is null) return null;

        // Reusing a key for a different operation means the client is confused; refusing is
        // safer than replaying a stock-in response to a stock-out request.
        if (!string.Equals(existing.Endpoint, endpoint, StringComparison.Ordinal))
            throw new ConflictException("That idempotency key was already used for a different operation.");

        return existing.ResponseJson is null
            ? throw new ConflictException("An identical request is still being processed.")
            : JsonSerializer.Deserialize<InventoryTransactionDto>(existing.ResponseJson);
    }

    /// <summary>
    /// Persists the operation and its idempotency record in one SaveChanges, so the key can
    /// never be recorded without its transaction or vice versa. If two identical requests race,
    /// the unique index on (TenantId, Key) fails the loser, which then replays the winner.
    /// </summary>
    private async Task<InventoryTransactionDto> SaveWithIdempotencyAsync(
        InventoryTransactionDto result,
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
                ResponseJson = JsonSerializer.Serialize(result)
            });
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            // Lost the race: the winning request already applied this key, so return its result.
            var replay = await TryReplayAsync(idempotencyKey, endpoint, cancellationToken);
            if (replay is not null) return replay;
            throw;
        }

        return result;
    }
}
