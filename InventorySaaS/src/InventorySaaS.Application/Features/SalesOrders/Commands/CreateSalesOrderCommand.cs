using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Sales;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.SalesOrders.Commands;

public record CreateSalesOrderCommand(
    Guid CustomerId,
    Guid WarehouseId,
    DateTime? DeliveryDate,
    string? ShippingAddress,
    string? Notes,
    List<CreateSalesOrderItemRequest> Items) : IRequest<Result<SalesOrderDto>>;

public class CreateSalesOrderCommandHandler : IRequestHandler<CreateSalesOrderCommand, Result<SalesOrderDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateSalesOrderCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SalesOrderDto>> Handle(CreateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Id == request.CustomerId, cancellationToken);

        if (customer is null)
            return Result<SalesOrderDto>.Failure("Customer not found.");

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);

        if (warehouse is null)
            return Result<SalesOrderDto>.Failure("Warehouse not found.");

        if (request.Items.Count == 0)
            return Result<SalesOrderDto>.Failure("At least one item is required.");

        var tenantId = _currentUserService.TenantId!.Value;

        // Auto-generate order number: SO-{YYYYMMDD}-{sequential}
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

        decimal subTotal = 0;
        decimal totalTax = 0;
        decimal totalDiscount = 0;

        var itemDtos = new List<SalesOrderItemDto>();

        foreach (var item in request.Items)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == item.ProductId, cancellationToken);

            if (product is null)
                return Result<SalesOrderDto>.Failure($"Product with ID {item.ProductId} not found.");

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
                orderItem.Id,
                product.Name,
                product.Sku,
                orderItem.Quantity,
                orderItem.DeliveredQuantity,
                orderItem.ReturnedQuantity,
                orderItem.UnitPrice,
                orderItem.LineTotal));
        }

        salesOrder.SubTotal = subTotal;
        salesOrder.TaxAmount = totalTax;
        salesOrder.DiscountAmount = totalDiscount;
        salesOrder.TotalAmount = subTotal + totalTax - totalDiscount;

        _context.SalesOrders.Add(salesOrder);
        await _context.SaveChangesAsync(cancellationToken);

        var dto = new SalesOrderDto(
            salesOrder.Id,
            salesOrder.OrderNumber,
            customer.Name,
            warehouse.Name,
            salesOrder.OrderDate,
            salesOrder.DeliveryDate,
            salesOrder.Status.ToString(),
            salesOrder.TotalAmount,
            itemDtos);

        return Result<SalesOrderDto>.Success(dto);
    }
}
