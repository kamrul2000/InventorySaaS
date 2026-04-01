using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.SalesOrders.Queries;

public record GetSalesOrderByIdQuery(Guid SalesOrderId) : IRequest<Result<SalesOrderDto>>;

public class GetSalesOrderByIdQueryHandler : IRequestHandler<GetSalesOrderByIdQuery, Result<SalesOrderDto>>
{
    private readonly IApplicationDbContext _context;

    public GetSalesOrderByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<SalesOrderDto>> Handle(GetSalesOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var so = await _context.SalesOrders
            .Include(s => s.Customer)
            .Include(s => s.Warehouse)
            .Include(s => s.Items)
                .ThenInclude(i => i.Product)
            .Where(s => s.Id == request.SalesOrderId && !s.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (so is null)
            return Result<SalesOrderDto>.Failure("Sales order not found.");

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
