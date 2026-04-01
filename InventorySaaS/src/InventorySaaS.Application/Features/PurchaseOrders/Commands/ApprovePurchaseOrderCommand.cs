using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.PurchaseOrders.Commands;

public record ApprovePurchaseOrderCommand(Guid PurchaseOrderId) : IRequest<Result<PurchaseOrderDto>>;

public class ApprovePurchaseOrderCommandHandler : IRequestHandler<ApprovePurchaseOrderCommand, Result<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public ApprovePurchaseOrderCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(ApprovePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);

        if (po is null)
            return Result<PurchaseOrderDto>.Failure("Purchase order not found.");

        if (po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Submitted)
            return Result<PurchaseOrderDto>.Failure($"Cannot approve a purchase order with status '{po.Status}'.");

        po.Status = PurchaseOrderStatus.Approved;
        await _context.SaveChangesAsync(cancellationToken);

        var itemDtos = po.Items.Select(i => new PurchaseOrderItemDto(
            i.Id,
            i.ProductId,
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
