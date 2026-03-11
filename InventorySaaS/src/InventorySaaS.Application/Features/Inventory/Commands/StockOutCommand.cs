using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Inventory.Commands;

public record StockOutCommand(
    Guid ProductId,
    Guid WarehouseId,
    Guid? LocationId,
    int Quantity,
    string? Notes) : IRequest<Result<InventoryTransactionDto>>;

public class StockOutCommandHandler : IRequestHandler<StockOutCommand, Result<InventoryTransactionDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public StockOutCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<InventoryTransactionDto>> Handle(StockOutCommand request, CancellationToken cancellationToken)
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

        // Find inventory balance
        var balance = await _context.InventoryBalances
            .FirstOrDefaultAsync(ib =>
                ib.ProductId == request.ProductId &&
                ib.WarehouseId == request.WarehouseId &&
                ib.LocationId == request.LocationId,
                cancellationToken);

        if (balance is null || balance.QuantityAvailable < request.Quantity)
            return Result<InventoryTransactionDto>.Failure("Insufficient stock available.");

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
