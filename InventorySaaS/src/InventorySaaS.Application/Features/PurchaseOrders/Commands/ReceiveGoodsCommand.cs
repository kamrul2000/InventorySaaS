using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Purchase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.PurchaseOrders.Commands;

public record ReceiveGoodsCommand(
    Guid PurchaseOrderId,
    List<ReceiveGoodsItemRequest> Items,
    string? Notes) : IRequest<Result<PurchaseOrderDto>>;

public class ReceiveGoodsCommandHandler : IRequestHandler<ReceiveGoodsCommand, Result<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ReceiveGoodsCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(ReceiveGoodsCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);

        if (po is null)
            return Result<PurchaseOrderDto>.Failure("Purchase order not found.");

        if (po.Status != PurchaseOrderStatus.Approved && po.Status != PurchaseOrderStatus.PartiallyReceived)
            return Result<PurchaseOrderDto>.Failure($"Cannot receive goods for a purchase order with status '{po.Status}'.");

        var tenantId = _currentUserService.TenantId!.Value;

        // Create goods receipt
        var receiptNumber = $"GR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
        var goodsReceipt = new GoodsReceipt
        {
            TenantId = tenantId,
            ReceiptNumber = receiptNumber,
            PurchaseOrderId = po.Id,
            ReceiptDate = DateTime.UtcNow,
            ReceivedBy = _currentUserService.Email,
            Notes = request.Notes
        };

        foreach (var receiveItem in request.Items)
        {
            var poItem = po.Items.FirstOrDefault(i => i.ProductId == receiveItem.ProductId);
            if (poItem is null)
                return Result<PurchaseOrderDto>.Failure($"Product {receiveItem.ProductId} is not part of this purchase order.");

            var remainingQuantity = poItem.Quantity - poItem.ReceivedQuantity;
            if (receiveItem.Quantity > remainingQuantity)
                return Result<PurchaseOrderDto>.Failure($"Cannot receive more than remaining quantity ({remainingQuantity}) for product {poItem.Product.Name}.");

            // Update PO item received quantity
            poItem.ReceivedQuantity += receiveItem.Quantity;

            // Create goods receipt item
            var grItem = new GoodsReceiptItem
            {
                TenantId = tenantId,
                GoodsReceiptId = goodsReceipt.Id,
                ProductId = receiveItem.ProductId,
                LocationId = receiveItem.LocationId,
                Quantity = receiveItem.Quantity,
                RejectedQuantity = receiveItem.RejectedQuantity,
                BatchNumber = receiveItem.BatchNumber,
                LotNumber = receiveItem.LotNumber,
                ExpiryDate = receiveItem.ExpiryDate
            };
            goodsReceipt.Items.Add(grItem);

            // Update inventory balance
            var acceptedQuantity = receiveItem.Quantity - receiveItem.RejectedQuantity;
            if (acceptedQuantity > 0)
            {
                var balance = await _context.InventoryBalances
                    .FirstOrDefaultAsync(ib =>
                        ib.ProductId == receiveItem.ProductId &&
                        ib.WarehouseId == po.WarehouseId &&
                        ib.LocationId == receiveItem.LocationId &&
                        ib.BatchNumber == receiveItem.BatchNumber,
                        cancellationToken);

                if (balance is null)
                {
                    balance = new InventoryBalance
                    {
                        TenantId = tenantId,
                        ProductId = receiveItem.ProductId,
                        WarehouseId = po.WarehouseId,
                        LocationId = receiveItem.LocationId,
                        BatchNumber = receiveItem.BatchNumber,
                        LotNumber = receiveItem.LotNumber,
                        ExpiryDate = receiveItem.ExpiryDate,
                        QuantityOnHand = 0,
                        UnitCost = poItem.UnitPrice
                    };
                    _context.InventoryBalances.Add(balance);
                }

                balance.QuantityOnHand += acceptedQuantity;
                balance.UnitCost = poItem.UnitPrice;

                // Create inventory transaction
                var txnNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..6].ToUpperInvariant()}";
                var transaction = new InventoryTransaction
                {
                    TenantId = tenantId,
                    TransactionNumber = txnNumber,
                    TransactionType = TransactionType.PurchaseReceive,
                    ProductId = receiveItem.ProductId,
                    WarehouseId = po.WarehouseId,
                    LocationId = receiveItem.LocationId,
                    Quantity = acceptedQuantity,
                    UnitCost = poItem.UnitPrice,
                    BatchNumber = receiveItem.BatchNumber,
                    LotNumber = receiveItem.LotNumber,
                    ExpiryDate = receiveItem.ExpiryDate,
                    ReferenceNumber = po.OrderNumber,
                    ReferenceType = "PurchaseOrder",
                    ReferenceId = po.Id,
                    Notes = $"Received from PO {po.OrderNumber}",
                    TransactionDate = DateTime.UtcNow
                };
                _context.InventoryTransactions.Add(transaction);
            }
        }

        _context.GoodsReceipts.Add(goodsReceipt);

        // Update PO status
        var allReceived = po.Items.All(i => i.ReceivedQuantity >= i.Quantity);
        var anyReceived = po.Items.Any(i => i.ReceivedQuantity > 0);

        po.Status = allReceived
            ? PurchaseOrderStatus.Received
            : anyReceived
                ? PurchaseOrderStatus.PartiallyReceived
                : po.Status;

        await _context.SaveChangesAsync(cancellationToken);

        var itemDtos = po.Items.Select(i => new PurchaseOrderItemDto(
            i.Id,
            i.Product.Name,
            i.Product.Sku,
            i.Quantity,
            i.ReceivedQuantity,
            i.UnitPrice,
            i.LineTotal)).ToList();

        var dto = new PurchaseOrderDto(
            po.Id,
            po.OrderNumber,
            po.Supplier.Name,
            po.Warehouse.Name,
            po.OrderDate,
            po.ExpectedDeliveryDate,
            po.Status.ToString(),
            po.TotalAmount,
            itemDtos);

        return Result<PurchaseOrderDto>.Success(dto);
    }
}
