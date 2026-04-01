using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.PurchaseOrders.Queries;

public record GetPurchaseOrderByIdQuery(Guid PurchaseOrderId) : IRequest<Result<PurchaseOrderDto>>;

public class GetPurchaseOrderByIdQueryHandler : IRequestHandler<GetPurchaseOrderByIdQuery, Result<PurchaseOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPurchaseOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PurchaseOrderDto>> Handle(GetPurchaseOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders
            .Include(p => p.Supplier)
            .Include(p => p.Warehouse)
            .Include(p => p.Items)
                .ThenInclude(i => i.Product)
            .Where(p => p.Id == request.PurchaseOrderId && !p.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (po is null)
            return Result<PurchaseOrderDto>.Failure("Purchase order not found.");

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
