using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Inventory.Commands;

public record StockAdjustmentCommand(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int NewQuantity,
    string Reason) : IRequest<Result<InventoryTransactionDto>>;

public class StockAdjustmentCommandHandler : IRequestHandler<StockAdjustmentCommand, Result<InventoryTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StockAdjustmentCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<InventoryTransactionDto>> Handle(StockAdjustmentCommand request, CancellationToken cancellationToken)
    {
        if (request.NewQuantity < 0)
            return Result<InventoryTransactionDto>.Failure("Quantity cannot be negative.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            return Result<InventoryTransactionDto>.Failure("Product not found.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);

        if (warehouse is null)
            return Result<InventoryTransactionDto>.Failure("Warehouse not found.");

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

        var dto = new InventoryTransactionDto(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.TransactionType.ToString(),
            product.Name,
            product.Sku,
            warehouse.Name,
            transaction.Quantity,
            transaction.UnitCost,
            transaction.BatchNumber,
            transaction.TransactionDate,
            transaction.Notes);

        return Result<InventoryTransactionDto>.Success(dto);
    }
}
