using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Inventory.Commands;

public record StockTransferCommand(
    Guid ProductId,
    Guid SourceWarehouseId,
    Guid? SourceLocationId,
    Guid DestinationWarehouseId,
    Guid? DestinationLocationId,
    int Quantity,
    string? Notes) : IRequest<Result<InventoryTransactionDto>>;

public class StockTransferCommandHandler : IRequestHandler<StockTransferCommand, Result<InventoryTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StockTransferCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<InventoryTransactionDto>> Handle(StockTransferCommand request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            return Result<InventoryTransactionDto>.Failure("Quantity must be greater than zero.");

        if (request.SourceWarehouseId == request.DestinationWarehouseId &&
            request.SourceLocationId == request.DestinationLocationId)
            return Result<InventoryTransactionDto>.Failure("Source and destination must be different.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            return Result<InventoryTransactionDto>.Failure("Product not found.");

        var sourceWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.SourceWarehouseId, cancellationToken);

        if (sourceWarehouse is null)
            return Result<InventoryTransactionDto>.Failure("Source warehouse not found.");

        var destWarehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.DestinationWarehouseId, cancellationToken);

        if (destWarehouse is null)
            return Result<InventoryTransactionDto>.Failure("Destination warehouse not found.");

        // Check source balance
        var sourceBalance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.SourceWarehouseId &&
                ib.LocationId == request.SourceLocationId,
                cancellationToken);

        if (sourceBalance is null || sourceBalance.QuantityAvailable < request.Quantity)
            return Result<InventoryTransactionDto>.Failure("Insufficient stock at source location.");

        var tenantId = _currentUserService.TenantId!.Value;

        // Deduct from source
        sourceBalance.QuantityOnHand -= request.Quantity;

        // Add to destination
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

        destBalance.QuantityOnHand += request.Quantity;

        // Create transfer transaction
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

        var dto = new InventoryTransactionDto(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.TransactionType.ToString(),
            product.Name,
            product.Sku,
            sourceWarehouse.Name,
            transaction.Quantity,
            transaction.UnitCost,
            transaction.BatchNumber,
            transaction.TransactionDate,
            transaction.Notes);

        return Result<InventoryTransactionDto>.Success(dto);
    }
}
