using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Purchase;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.PurchaseOrders.Commands;

public record CreatePurchaseOrderCommand(
    Guid SupplierId,
    Guid WarehouseId,
    DateTime? ExpectedDeliveryDate,
    string? Notes,
    List<CreatePurchaseOrderItemRequest> Items) : IRequest<Result<PurchaseOrderDto>>;

public class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreatePurchaseOrderCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var supplier = await _context.Suppliers
            .FirstOrDefaultAsync(s => s.Id == request.SupplierId, cancellationToken);

        if (supplier is null)
            return Result<PurchaseOrderDto>.Failure("Supplier not found.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);

        if (warehouse is null)
            return Result<PurchaseOrderDto>.Failure("Warehouse not found.");

        if (request.Items.Count == 0)
            return Result<PurchaseOrderDto>.Failure("At least one item is required.");

        var tenantId = _currentUserService.TenantId!.Value;

        // Auto-generate order number: PO-{YYYYMMDD}-{sequential}
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

        decimal subTotal = 0;
        decimal totalTax = 0;
        decimal totalDiscount = 0;

        var itemDtos = new List<PurchaseOrderItemDto>();

        foreach (var item in request.Items)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);

            if (product is null)
                return Result<PurchaseOrderDto>.Failure($"Product with ID {item.ProductId} not found.");

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
                orderItem.Id,
                product.Name,
                product.Sku,
                orderItem.Quantity,
                orderItem.ReceivedQuantity,
                orderItem.UnitPrice,
                orderItem.LineTotal));
        }

        purchaseOrder.SubTotal = subTotal;
        purchaseOrder.TaxAmount = totalTax;
        purchaseOrder.DiscountAmount = totalDiscount;
        purchaseOrder.TotalAmount = subTotal + totalTax - totalDiscount;

        _context.PurchaseOrders.Add(purchaseOrder);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new PurchaseOrderDto(
            purchaseOrder.Id,
            purchaseOrder.OrderNumber,
            supplier.Name,
            warehouse.Name,
            purchaseOrder.OrderDate,
            purchaseOrder.ExpectedDeliveryDate,
            purchaseOrder.Status.ToString(),
            purchaseOrder.TotalAmount,
            itemDtos);

        return Result<PurchaseOrderDto>.Success(dto);
    }
}
