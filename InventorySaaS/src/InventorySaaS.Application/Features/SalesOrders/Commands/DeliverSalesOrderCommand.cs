using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.SalesOrders.Commands;

public record DeliverSalesOrderCommand(
    Guid SalesOrderId,
    List<DeliverSalesOrderItemRequest> Items,
    string? Notes) : IRequest<Result<SalesOrderDto>>;

public class DeliverSalesOrderCommandHandler : IRequestHandler<DeliverSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeliverSalesOrderCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SalesOrderDto>> Handle(DeliverSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId, cancellationToken);

        if (so is null)
            return Result<SalesOrderDto>.Failure("Sales order not found.");

        if (so.Status != SalesOrderStatus.Confirmed && so.Status != SalesOrderStatus.PartiallyDelivered)
            return Result<SalesOrderDto>.Failure($"Cannot deliver a sales order with status '{so.Status}'.");

        var tenantId = _currentUserService.TenantId!.Value;

        foreach (var deliverItem in request.Items)
        {
            var soItem = so.Items.FirstOrDefault(i => i.ProductId == deliverItem.ProductId);
            if (soItem is null)
                return Result<SalesOrderDto>.Failure($"Product {deliverItem.ProductId} is not part of this sales order.");

            var remainingToDeliver = soItem.Quantity - soItem.DeliveredQuantity;
            if (deliverItem.Quantity > remainingToDeliver)
                return Result<SalesOrderDto>.Failure($"Cannot deliver more than remaining quantity ({remainingToDeliver}) for product {soItem.Product.Name}.");

            soItem.DeliveredQuantity += deliverItem.Quantity;

            // Deduct from inventory (release reservation and reduce on-hand)
            var balances = await _context.InventoryBalances
                .Where(ib => ib.ProductId == deliverItem.ProductId && ib.WarehouseId == so.WarehouseId)
                .OrderBy(ib => ib.ExpiryDate)
                .ToListAsync(cancellationToken);

            var remainingToDeduct = deliverItem.Quantity;
            foreach (var balance in balances)
            {
                if (remainingToDeduct <= 0) break;

                var toDeduct = Math.Min(balance.QuantityOnHand, remainingToDeduct);
                balance.QuantityOnHand -= toDeduct;
                balance.QuantityReserved = Math.Max(0, balance.QuantityReserved - toDeduct);
                remainingToDeduct -= toDeduct;
            }

            // Create inventory transaction
            var txnNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
            var transaction = new InventoryTransaction
            {
                TenantId = tenantId,
                TransactionNumber = txnNumber,
                TransactionType = TransactionType.SalesIssue,
                ProductId = deliverItem.ProductId,
                WarehouseId = so.WarehouseId,
                Quantity = deliverItem.Quantity,
                UnitCost = soItem.UnitPrice,
                ReferenceNumber = so.OrderNumber,
                ReferenceType = "SalesOrder",
                ReferenceId = so.Id,
                Notes = request.Notes ?? $"Delivered for SO {so.OrderNumber}",
                TransactionDate = DateTime.UtcNow
            };
            _context.InventoryTransactions.Add(transaction);
        }

        // Update SO status
        var allDelivered = so.Items.All(i => i.DeliveredQuantity >= i.Quantity);
        var anyDelivered = so.Items.Any(i => i.DeliveredQuantity > 0);

        so.Status = allDelivered
            ? SalesOrderStatus.Delivered
            : anyDelivered
                ? SalesOrderStatus.PartiallyDelivered
                : so.Status;

        if (allDelivered)
            so.DeliveryDate = DateTime.UtcNow;

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
