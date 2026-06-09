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
            ib.WarehouseId, ib.Warehouse.Name, ib.Location != null ? ib.Location.Name : null,
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
            t.Quantity, t.UnitCost, t.BatchNumber, t.TransactionDate, t.Notes));

        return await PaginatedList<InventoryTransactionDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<InventoryTransactionDto> StockInAsync(
        StockInRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

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
                ib.LocationId == request.LocationId &&
                ib.BatchNumber == request.BatchNumber,
                cancellationToken);

        if (balance is null)
        {
            balance = new InventoryBalance
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.WarehouseId,
                LocationId = request.LocationId,
                BatchNumber = request.BatchNumber,
                LotNumber = request.LotNumber,
                ExpiryDate = request.ExpiryDate,
                QuantityOnHand = 0,
                UnitCost = request.UnitCost
            };
            _context.InventoryBalances.Add(balance);
        }

        // Recompute moving weighted-average cost rather than overwriting with the latest cost.
        balance.ApplyInbound(request.Quantity, request.UnitCost);

        var transactionNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        var transaction = new InventoryTransaction
        {
            TenantId = tenantId,
            TransactionNumber = transactionNumber,
            TransactionType = TransactionType.StockIn,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            Quantity = request.Quantity,
            UnitCost = request.UnitCost,
            BatchNumber = request.BatchNumber,
            LotNumber = request.LotNumber,
            ExpiryDate = request.ExpiryDate,
            Notes = request.Notes,
            TransactionDate = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return new InventoryTransactionDto(
            transaction.Id, transaction.TransactionNumber, transaction.TransactionType.ToString(),
            product.Name, product.Sku, warehouse.Name,
            transaction.Quantity, transaction.UnitCost, transaction.BatchNumber,
            transaction.TransactionDate, transaction.Notes);
    }

    public async Task<InventoryTransactionDto> StockOutAsync(
        StockOutRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            throw new BadRequestException("Quantity must be greater than zero.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken)
            ?? throw new NotFoundException(nameof(ProductInfo), request.ProductId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        var balance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.WarehouseId &&
                ib.LocationId == request.LocationId,
                cancellationToken);

        if (balance is null || balance.QuantityAvailable < request.Quantity)
            throw new BadRequestException("Insufficient stock available.");

        balance.QuantityOnHand -= request.Quantity;

        var transactionNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        var transaction = new InventoryTransaction
        {
            TenantId = _currentUserService.TenantId!.Value,
            TransactionNumber = transactionNumber,
            TransactionType = TransactionType.StockOut,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            Quantity = request.Quantity,
            UnitCost = balance.UnitCost,
            Notes = request.Notes,
            TransactionDate = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return new InventoryTransactionDto(
            transaction.Id, transaction.TransactionNumber, transaction.TransactionType.ToString(),
            product.Name, product.Sku, warehouse.Name,
            transaction.Quantity, transaction.UnitCost, transaction.BatchNumber,
            transaction.TransactionDate, transaction.Notes);
    }

    public async Task<InventoryTransactionDto> TransferAsync(
        StockTransferRequest request,
        CancellationToken cancellationToken)
    {
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

        var destWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.DestinationWarehouseId, cancellationToken)
            ?? throw new NotFoundException("DestinationWarehouse", request.DestinationWarehouseId);

        var sourceBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.SourceWarehouseId &&
                ib.LocationId == request.SourceLocationId,
                cancellationToken);

        if (sourceBalance is null || sourceBalance.QuantityAvailable < request.Quantity)
            throw new BadRequestException("Insufficient stock at source location.");

        var tenantId = _currentUserService.TenantId!.Value;

        sourceBalance.QuantityOnHand -= request.Quantity;

        var destBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.DestinationWarehouseId &&
                ib.LocationId == request.DestinationLocationId,
                cancellationToken);

        if (destBalance is null)
        {
            destBalance = new InventoryBalance
            {
                TenantId = tenantId,
                ProductId = request.ProductId,
                WarehouseId = request.DestinationWarehouseId,
                LocationId = request.DestinationLocationId,
                BatchNumber = sourceBalance.BatchNumber,
                LotNumber = sourceBalance.LotNumber,
                ExpiryDate = sourceBalance.ExpiryDate,
                QuantityOnHand = 0,
                UnitCost = sourceBalance.UnitCost
            };
            _context.InventoryBalances.Add(destBalance);
        }

        // Carry the source cost into the destination via weighted-average blend.
        destBalance.ApplyInbound(request.Quantity, sourceBalance.UnitCost);

        var transactionNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        var transaction = new InventoryTransaction
        {
            TenantId = tenantId,
            TransactionNumber = transactionNumber,
            TransactionType = TransactionType.Transfer,
            ProductId = request.ProductId,
            WarehouseId = request.SourceWarehouseId,
            LocationId = request.SourceLocationId,
            DestinationWarehouseId = request.DestinationWarehouseId,
            DestinationLocationId = request.DestinationLocationId,
            Quantity = request.Quantity,
            UnitCost = sourceBalance.UnitCost,
            Notes = request.Notes,
            TransactionDate = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return new InventoryTransactionDto(
            transaction.Id, transaction.TransactionNumber, transaction.TransactionType.ToString(),
            product.Name, product.Sku, sourceWarehouse.Name,
            transaction.Quantity, transaction.UnitCost, transaction.BatchNumber,
            transaction.TransactionDate, transaction.Notes);
    }

    public async Task<InventoryTransactionDto> AdjustAsync(
        StockAdjustmentRequest request,
        CancellationToken cancellationToken)
    {
        if (request.NewQuantity < 0)
            throw new BadRequestException("Quantity cannot be negative.");

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

        var transactionNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";

        var transaction = new InventoryTransaction
        {
            TenantId = tenantId,
            TransactionNumber = transactionNumber,
            TransactionType = TransactionType.Adjustment,
            ProductId = request.ProductId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            Quantity = adjustmentQuantity,
            UnitCost = balance.UnitCost,
            Notes = $"Adjustment: {previousQuantity} -> {request.NewQuantity}. Reason: {request.Reason}",
            TransactionDate = DateTime.UtcNow
        };

        _context.InventoryTransactions.Add(transaction);
        await _context.SaveChangesAsync(cancellationToken);

        return new InventoryTransactionDto(
            transaction.Id, transaction.TransactionNumber, transaction.TransactionType.ToString(),
            product.Name, product.Sku, warehouse.Name,
            transaction.Quantity, transaction.UnitCost, transaction.BatchNumber,
            transaction.TransactionDate, transaction.Notes);
    }
}
