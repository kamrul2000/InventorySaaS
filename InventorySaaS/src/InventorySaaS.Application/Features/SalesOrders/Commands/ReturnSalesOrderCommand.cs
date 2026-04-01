using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.SalesOrders.Commands;

public record ReturnSalesOrderCommand(
    Guid SalesOrderId,
    List<ReturnSalesOrderItemRequest> Items,
    string? Reason) : IRequest<Result<SalesOrderDto>>;

public class ReturnSalesOrderCommandHandler : IRequestHandler<ReturnSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ReturnSalesOrderCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SalesOrderDto>> Handle(ReturnSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId, cancellationToken);

        if (so is null)
            return Result<SalesOrderDto>.Failure("Sales order not found.");

        if (so.Status != SalesOrderStatus.Delivered && so.Status != SalesOrderStatus.PartiallyDelivered)
            return Result<SalesOrderDto>.Failure($"Cannot process returns for a sales order with status '{so.Status}'.");

        var tenantId = _currentUserService.TenantId!.Value;

        foreach (var returnItem in request.Items)
        {
            var soItem = so.Items.FirstOrDefault(i => i.ProductId == returnItem.ProductId);
            if (soItem is null)
                return Result<SalesOrderDto>.Failure($"Product {returnItem.ProductId} is not part of this sales order.");

            var maxReturnable = soItem.DeliveredQuantity - soItem.ReturnedQuantity;
            if (returnItem.Quantity > maxReturnable)
                return Result<SalesOrderDto>.Failure($"Cannot return more than delivered quantity ({maxReturnable}) for product {soItem.Product.Name}.");

            soItem.ReturnedQuantity += returnItem.Quantity;

            // Add back to inventory
            var balance = await _context.InventoryBalances
                .FirstOrDefaultAsync(ib =>
                    ib.ProductId == returnItem.ProductId &&
                    ib.WarehouseId == so.WarehouseId,
                    cancellationToken);

            if (balance is not null)
            {
                balance.QuantityOnHand += returnItem.Quantity;
            }
            else
            {
                balance = new InventoryBalance
                {
                    TenantId = tenantId,
                    ProductId = returnItem.ProductId,
                    WarehouseId = so.WarehouseId,
                    QuantityOnHand = returnItem.Quantity,
                    UnitCost = soItem.UnitPrice
                };
                _context.InventoryBalances.Add(balance);
            }

            // Create return transaction
            var txnNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
            var transaction = new InventoryTransaction
            {
                TenantId = tenantId,
                TransactionNumber = txnNumber,
                TransactionType = TransactionType.Return,
                ProductId = returnItem.ProductId,
                WarehouseId = so.WarehouseId,
                Quantity = returnItem.Quantity,
                UnitCost = soItem.UnitPrice,
                ReferenceNumber = so.OrderNumber,
                ReferenceType = "SalesOrder",
                ReferenceId = so.Id,
                Notes = $"Return from SO {so.OrderNumber}. Reason: {returnItem.Reason ?? request.Reason ?? "N/A"}",
                TransactionDate = DateTime.UtcNow
            };
            _context.InventoryTransactions.Add(transaction);
        }

        // Check if all delivered items have been returned
        var allReturned = so.Items.All(i => i.ReturnedQuantity >= i.DeliveredQuantity);
        if (allReturned)
            so.Status = SalesOrderStatus.Returned;

        await _context.SaveChangesAsync(cancellationToken);

        var itemDtos = so.Items.Select(i => new SalesOrderItemDto(
            i.Id,
            i.ProductId,
            i.Product.Name,
            i.Product.Sku,
            i.Quantity,
            i.DeliveredQuantity,
            i.ReturnedQuantity,
            i.UnitPrice,
            i.LineTotal)).ToList();

        var dto = new SalesOrderDto(
            so.Id,
            so.OrderNumber,
            so.Customer.Name,
            so.Warehouse.Name,
            so.OrderDate,
            so.DeliveryDate,
            so.Status.ToString(),
            so.TotalAmount,
            itemDtos);

        return Result<SalesOrderDto>.Success(dto);
    }
}
