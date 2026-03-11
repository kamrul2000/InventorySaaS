using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Inventory.Commands;

public record StockInCommand(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    decimal UnitCost,
    string? BatchNumber,
    string? LotNumber,
    DateTime? ExpiryDate,
    string? Notes) : IRequest<Result<InventoryTransactionDto>>;

public class StockInCommandHandler : IRequestHandler<StockInCommand, Result<InventoryTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StockInCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<InventoryTransactionDto>> Handle(StockInCommand request, CancellationToken cancellationToken)
    {
        if (request.Quantity <= 0)
            return Result<InventoryTransactionDto>.Failure("Quantity must be greater than zero.");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId, cancellationToken);

        if (product is null)
            return Result<InventoryTransactionDto>.Failure("Product not found.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);

        if (warehouse is null)
            return Result<InventoryTransactionDto>.Failure("Warehouse not found.");

        var tenantId = _currentUserService.TenantId!.Value;

        // Find or create inventory balance
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

        balance.QuantityOnHand += request.Quantity;
        balance.UnitCost = request.UnitCost;

        // Create transaction record
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
