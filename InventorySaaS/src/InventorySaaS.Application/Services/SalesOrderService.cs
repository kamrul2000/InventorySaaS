using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Entities.Warehouse;
using InventorySaaS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class SalesOrderService : ISalesOrderService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SalesOrderService(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<PaginatedList<SalesOrderDto>> GetAllAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Warehouse)
            .Include(so => so.Items)
                .ThenInclude(i => i.Product)
            .Where(so => !so.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(so =>
                so.OrderNumber.ToLower().Contains(searchTerm) ||
                so.Customer.Name.ToLower().Contains(searchTerm));
        }

        query = pagination.SortBy?.ToLowerInvariant() switch
        {
            "ordernumber" => pagination.SortDescending ? query.OrderByDescending(so => so.OrderNumber) : query.OrderBy(so => so.OrderNumber),
            "customer" => pagination.SortDescending ? query.OrderByDescending(so => so.Customer.Name) : query.OrderBy(so => so.Customer.Name),
            "status" => pagination.SortDescending ? query.OrderByDescending(so => so.Status) : query.OrderBy(so => so.Status),
            "amount" => pagination.SortDescending ? query.OrderByDescending(so => so.TotalAmount) : query.OrderBy(so => so.TotalAmount),
            _ => query.OrderByDescending(so => so.OrderDate)
        };

        var projected = query.Select(so => new SalesOrderDto(
            so.Id, so.OrderNumber, so.Customer.Name, so.Warehouse.Name,
            so.OrderDate, so.DeliveryDate, so.Status.ToString(), so.TotalAmount,
            so.Items.Select(i => new SalesOrderItemDto(
                i.Id, i.ProductId, i.Product.Name, i.Product.Sku,
                i.Quantity, i.DeliveredQuantity, i.ReturnedQuantity,
                i.UnitPrice, i.LineTotal)).ToList()));

        return await PaginatedList<SalesOrderDto>.CreateAsync(
            projected, pagination.PageNumber, pagination.PageSize, cancellationToken);
    }

    public async Task<SalesOrderDto> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .Where(s => s.Id == id && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), id);

        return ToDto(so);
    }

    public async Task<SalesOrderDto> CreateAsync(
        CreateSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken)
            ?? throw new NotFoundException(nameof(CustomerInfo), request.CustomerId);

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken)
            ?? throw new NotFoundException(nameof(WarehouseInfo), request.WarehouseId);

        if (request.Items.Count == 0)
            throw new BadRequestException("At least one item is required.");

        var tenantId = _currentUserService.TenantId!.Value;

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var todayCount = await _context.SalesOrders
            .Where(so => so.OrderNumber.StartsWith($"SO-{today}"))
            .CountAsync(cancellationToken);

        var orderNumber = $"SO-{today}-{(todayCount + 1):D4}";

        var salesOrder = new SalesOrder
        {
            TenantId = tenantId,
            OrderNumber = orderNumber,
            CustomerId = request.CustomerId,
            WarehouseId = request.WarehouseId,
            OrderDate = DateTime.UtcNow,
            DeliveryDate = request.DeliveryDate,
            ShippingAddress = request.ShippingAddress,
            Status = SalesOrderStatus.Draft,
            Notes = request.Notes
        };

        decimal subTotal = 0, totalTax = 0, totalDiscount = 0;
        var itemDtos = new List<SalesOrderItemDto>();

        foreach (var item in request.Items)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken)
                ?? throw new BadRequestException($"Product with ID {item.ProductId} not found.");

            var lineSubTotal = item.Quantity * item.UnitPrice;
            var lineTax = lineSubTotal * (item.TaxRate / 100m);
            var lineDiscount = lineSubTotal * (item.DiscountRate / 100m);
            var lineTotal = lineSubTotal + lineTax - lineDiscount;

            var orderItem = new SalesOrderItem
            {
                TenantId = tenantId,
                SalesOrderId = salesOrder.Id,
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                DeliveredQuantity = 0,
                ReturnedQuantity = 0,
                UnitPrice = item.UnitPrice,
                TaxRate = item.TaxRate,
                DiscountRate = item.DiscountRate,
                LineTotal = lineTotal
            };

            salesOrder.Items.Add(orderItem);

            subTotal += lineSubTotal;
            totalTax += lineTax;
            totalDiscount += lineDiscount;

            itemDtos.Add(new SalesOrderItemDto(
                orderItem.Id, item.ProductId, product.Name, product.Sku,
                orderItem.Quantity, orderItem.DeliveredQuantity, orderItem.ReturnedQuantity,
                orderItem.UnitPrice, orderItem.LineTotal));
        }

        salesOrder.SubTotal = subTotal;
        salesOrder.TaxAmount = totalTax;
        salesOrder.DiscountAmount = totalDiscount;
        salesOrder.TotalAmount = subTotal + totalTax - totalDiscount;

        _context.SalesOrders.Add(salesOrder);
        await _context.SaveChangesAsync(cancellationToken);

        return new SalesOrderDto(
            salesOrder.Id, salesOrder.OrderNumber, customer.Name, warehouse.Name,
            salesOrder.OrderDate, salesOrder.DeliveryDate,
            salesOrder.Status.ToString(), salesOrder.TotalAmount, itemDtos);
    }

    public async Task<SalesOrderDto> ConfirmAsync(Guid id, CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), id);

        if (so.Status != SalesOrderStatus.Draft)
            throw new BadRequestException($"Cannot confirm a sales order with status '{so.Status}'.");

        foreach (var item in so.Items)
        {
            var availableStock = await _context.InventoryBalances
                .Where(ib => ib.ProductId == item.ProductId && ib.WarehouseId == so.WarehouseId)
                .SumAsync(ib => ib.QuantityOnHand - ib.QuantityReserved, cancellationToken);

            if (availableStock < item.Quantity)
                throw new BadRequestException($"Insufficient stock for product '{item.Product.Name}'. Available: {availableStock}, Required: {item.Quantity}.");
        }

        foreach (var item in so.Items)
        {
            var balances = await _context.InventoryBalances
                .Where(ib => ib.ProductId == item.ProductId && ib.WarehouseId == so.WarehouseId)
                .OrderBy(ib => ib.ExpiryDate)
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

        return ToDto(so);
    }

    public async Task<SalesOrderDto> DeliverAsync(
        Guid id,
        DeliverSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), id);

        if (so.Status != SalesOrderStatus.Confirmed && so.Status != SalesOrderStatus.PartiallyDelivered)
            throw new BadRequestException($"Cannot deliver a sales order with status '{so.Status}'.");

        var tenantId = _currentUserService.TenantId!.Value;

        foreach (var deliverItem in request.Items)
        {
            var soItem = so.Items.FirstOrDefault(i => i.ProductId == deliverItem.ProductId)
                ?? throw new BadRequestException($"Product {deliverItem.ProductId} is not part of this sales order.");

            var remainingToDeliver = soItem.Quantity - soItem.DeliveredQuantity;
            if (deliverItem.Quantity > remainingToDeliver)
                throw new BadRequestException($"Cannot deliver more than remaining quantity ({remainingToDeliver}) for product {soItem.Product.Name}.");

            soItem.DeliveredQuantity += deliverItem.Quantity;

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

        return ToDto(so);
    }

    public async Task<SalesOrderDto> ReturnAsync(
        Guid id,
        ReturnSalesOrderRequest request,
        CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new NotFoundException(nameof(SalesOrder), id);

        if (so.Status != SalesOrderStatus.Delivered && so.Status != SalesOrderStatus.PartiallyDelivered)
            throw new BadRequestException($"Cannot process returns for a sales order with status '{so.Status}'.");

        var tenantId = _currentUserService.TenantId!.Value;

        foreach (var returnItem in request.Items)
        {
            var soItem = so.Items.FirstOrDefault(i => i.ProductId == returnItem.ProductId)
                ?? throw new BadRequestException($"Product {returnItem.ProductId} is not part of this sales order.");

            var maxReturnable = soItem.DeliveredQuantity - soItem.ReturnedQuantity;
            if (returnItem.Quantity > maxReturnable)
                throw new BadRequestException($"Cannot return more than delivered quantity ({maxReturnable}) for product {soItem.Product.Name}.");

            soItem.ReturnedQuantity += returnItem.Quantity;

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

        var allReturned = so.Items.All(i => i.ReturnedQuantity >= i.DeliveredQuantity);
        if (allReturned)
            so.Status = SalesOrderStatus.Returned;

        await _context.SaveChangesAsync(cancellationToken);

        return ToDto(so);
    }

    private static SalesOrderDto ToDto(SalesOrder so) => new(
        so.Id, so.OrderNumber, so.Customer.Name, so.Warehouse.Name,
        so.OrderDate, so.DeliveryDate, so.Status.ToString(), so.TotalAmount,
        so.Items.Select(i => new SalesOrderItemDto(
            i.Id, i.ProductId, i.Product.Name, i.Product.Sku,
            i.Quantity, i.DeliveredQuantity, i.ReturnedQuantity,
            i.UnitPrice, i.LineTotal)).ToList());
}
