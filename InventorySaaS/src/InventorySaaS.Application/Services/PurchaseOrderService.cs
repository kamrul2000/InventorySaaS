using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Purchase;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public PurchaseOrderService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<PurchaseOrderDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .Include(po => po.Items)
                .ThenInclude(i => i.Product)
            .Where(po => !po.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(po =>
                po.OrderNumber.ToLower().Contains(searchTerm) ||
                po.Supplier.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "ordernumber" => pagination.SortDescending ? query.OrderByDescending(po => po.OrderNumber) : query.OrderBy(po => po.OrderNumber),
            "supplier" => pagination.SortDescending ? query.OrderByDescending(po => po.Supplier.Name) : query.OrderBy(po => po.Supplier.Name),
            "status" => pagination.SortDescending ? query.OrderByDescending(po => po.Status) : query.OrderBy(po => po.Status),
            "amount" => pagination.SortDescending ? query.OrderByDescending(po => po.TotalAmount) : query.OrderBy(po => po.TotalAmount),
            _ => query.OrderByDescending(po => po.OrderDate)
        };

        var projected = query.Select(po => new PurchaseOrderDto(
            po.Id, po.OrderNumber, po.Supplier.Name, po.Warehouse.Name,
            po.OrderDate, po.ExpectedDeliveryDate, po.Status.ToString(), po.TotalAmount,
            po.Items.Select(i => new PurchaseOrderItemDto(
                i.Id, i.ProductId, i.Product.Name, i.Product.Sku,
                i.Quantity, i.ReceivedQuantity, i.UnitPrice, i.LineTotal)).ToList()));

        return await PaginatedList<PurchaseOrderDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<PurchaseOrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .Where(p => p.Id == id && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), id);

        return ToDto(po);
    }

    public async Task<PurchaseOrderDto> CreateAsync(
        CreatePurchaseOrderRequest request,
        CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken)
            ?? throw new NotFoundException(nameof(SupplierInfo), request.SupplierId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        if (request.Items.Count == 0)
            throw new BadRequestException("At least one item is required.");

        var tenantId = _currentUserService.TenantId!.Value;

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.PurchaseOrders
            .Where(po => po.OrderNumber.StartsWith($"PO-{today}"))
            .CountAsync(cancellationToken);

        var orderNumber = $"PO-{today}-{(todayCount + 1):D4}";

        var purchaseOrder = new PurchaseOrder
        {
            TenantId = tenantId,
            OrderNumber = orderNumber,
            SupplierId = request.SupplierId,
            WarehouseId = request.WarehouseId,
            OrderDate = DateTime.UtcNow,
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            Status = PurchaseOrderStatus.Draft,
            Notes = request.Notes
        };

        decimal subTotal = 0, totalTax = 0, totalDiscount = 0;
        var itemDtos = new List<PurchaseOrderItemDto>();

        foreach (var item in request.Items)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken)
                ?? throw new BadRequestException($"Product with ID {item.ProductId} not found.");

            var lineSubTotal = item.Quantity * item.UnitPrice;
            var lineTax = lineSubTotal * (item.TaxRate / 100m);
            var lineDiscount = lineSubTotal * (item.DiscountRate / 100m);
            var lineTotal = lineSubTotal + lineTax - lineDiscount;

            var orderItem = new PurchaseOrderItem
            {
                TenantId = tenantId,
                PurchaseOrderId = purchaseOrder.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                ReceivedQuantity = 0,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                DiscountRate = item.DiscountRate,
                LineTotal = lineTotal
            };

            purchaseOrder.Items.Add(orderItem);

            subTotal += lineSubTotal;
            totalTax += lineTax;
            totalDiscount += lineDiscount;

            itemDtos.Add(new PurchaseOrderItemDto(
                orderItem.Id, item.ProductId, product.Name, product.Sku,
                orderItem.Quantity, orderItem.ReceivedQuantity, orderItem.UnitPrice, orderItem.LineTotal));
        }

        purchaseOrder.SubTotal = subTotal;
        purchaseOrder.TaxAmount = totalTax;
        purchaseOrder.DiscountAmount = totalDiscount;
        purchaseOrder.TotalAmount = subTotal + totalTax - totalDiscount;

        _context.PurchaseOrders.Add(purchaseOrder);
        await _context.SaveChangesAsync(cancellationToken);

        return new PurchaseOrderDto(
            purchaseOrder.Id, purchaseOrder.OrderNumber, supplier.Name, warehouse.Name,
            purchaseOrder.OrderDate, purchaseOrder.ExpectedDeliveryDate,
            purchaseOrder.Status.ToString(), purchaseOrder.TotalAmount, itemDtos);
    }

    public async Task<PurchaseOrderDto> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), id);

        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Submitted)
            throw new BadRequestException($"Cannot approve a purchase order with status '{po.Status}'.");

        po.Status = PurchaseOrderStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(po);
    }

    public async Task<PurchaseOrderDto> ReceiveAsync(
        Guid id,
        ReceiveGoodsRequest request,
        CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(PurchaseOrder), id);

        if (po.Status != PurchaseOrderStatus.Approved && po.Status != PurchaseOrderStatus.PartiallyReceived)
            throw new BadRequestException($"Cannot receive goods for a purchase order with status '{po.Status}'.");

        var tenantId = _currentUserService.TenantId!.Value;

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
            var poItem = po.Items.FirstOrDefault(i => i.ProductId == receiveItem.ProductId)
                ?? throw new BadRequestException($"Product {receiveItem.ProductId} is not part of this purchase order.");

            var remainingQuantity = poItem.Quantity - poItem.ReceivedQuantity;
            if (receiveItem.Quantity > remainingQuantity)
                throw new BadRequestException($"Cannot receive more than remaining quantity ({remainingQuantity}) for product {poItem.Product.Name}.");

            poItem.ReceivedQuantity += receiveItem.Quantity;

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

        var allReceived = po.Items.All(i => i.ReceivedQuantity >= i.Quantity);
        var anyReceived = po.Items.Any(i => i.ReceivedQuantity > 0);

        po.Status = allReceived
            ? PurchaseOrderStatus.Received
            : anyReceived
                ? PurchaseOrderStatus.PartiallyReceived
                : po.Status;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(po);
    }

    private static PurchaseOrderDto ToDto(PurchaseOrder po) => new(
        po.Id, po.OrderNumber, po.Supplier.Name, po.Warehouse.Name,
        po.OrderDate, po.ExpectedDeliveryDate, po.Status.ToString(), po.TotalAmount,
        po.Items.Select(i => new PurchaseOrderItemDto(
            i.Id, i.ProductId, i.Product.Name, i.Product.Sku,
            i.Quantity, i.ReceivedQuantity, i.UnitPrice, i.LineTotal)).ToList());
}
