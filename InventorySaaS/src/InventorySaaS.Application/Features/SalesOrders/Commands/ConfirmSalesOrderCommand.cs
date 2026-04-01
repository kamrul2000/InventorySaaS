using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.SalesOrders.Commands;

public record ConfirmSalesOrderCommand(Guid SalesOrderId) : IRequest<Result<SalesOrderDto>>;

public class ConfirmSalesOrderCommandHandler : IRequestHandler<ConfirmSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public ConfirmSalesOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SalesOrderDto>> Handle(ConfirmSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == request.SalesOrderId, cancellationToken);

        if (so is null)
            return Result<SalesOrderDto>.Failure("Sales order not found.");

        if (so.Status != SalesOrderStatus.Draft)
            return Result<SalesOrderDto>.Failure($"Cannot confirm a sales order with status '{so.Status}'.");

        // Check stock availability for all items
        foreach (var item in so.Items)
        {
            var availableStock = await _context.InventoryBalances
                .Where(ib => ib.ProductId == item.ProductId && ib.WarehouseId == so.WarehouseId)
                .SumAsync(ib => ib.QuantityOnHand - ib.QuantityReserved, cancellationToken);

            if (availableStock < item.Quantity)
                return Result<SalesOrderDto>.Failure($"Insufficient stock for product '{item.Product.Name}'. Available: {availableStock}, Required: {item.Quantity}.");
        }

        // Reserve stock
        foreach (var item in so.Items)
        {
            var balances = await _context.InventoryBalances
                .Where(ib => ib.ProductId == item.ProductId && ib.WarehouseId == so.WarehouseId)
                .OrderBy(ib => ib.ExpiryDate) // FEFO: First Expiry, First Out
                .ToListAsync(cancellationToken);

            var remainingToReserve = item.Quantity;
            foreach (var balance in balances)
            {
                if (remainingToReserve <= 0) break;

                var available = balance.QuantityOnHand - balance.QuantityReserved;
                var toReserve = Math.Min(available, remainingToReserve);

                balance.QuantityReserved += toReserve;
                remainingToReserve -= toReserve;
            }
        }

        so.Status = SalesOrderStatus.Confirmed;
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
