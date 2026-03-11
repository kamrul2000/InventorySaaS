using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.PurchaseOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.PurchaseOrders.Queries;

public record GetPurchaseOrdersQuery(PaginationParams Pagination) : IRequest<Result<PaginatedList<PurchaseOrderDto>>>;

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, Result<PaginatedList<PurchaseOrderDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetPurchaseOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<PurchaseOrderDto>>> Handle(GetPurchaseOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.PurchaseOrders
            .Include(po => po.Supplier)
            .Include(po => po.Warehouse)
            .Include(po => po.Items)
                .ThenInclude(i => i.Product)
            .Where(po => !po.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(po =>
                po.OrderNumber.ToLower().Contains(searchTerm) ||
                po.Supplier.Name.ToLower().Contains(searchTerm));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "ordernumber" => request.Pagination.SortDescending ? query.OrderByDescending(po => po.OrderNumber) : query.OrderBy(po => po.OrderNumber),
            "supplier" => request.Pagination.SortDescending ? query.OrderByDescending(po => po.Supplier.Name) : query.OrderBy(po => po.Supplier.Name),
            "status" => request.Pagination.SortDescending ? query.OrderByDescending(po => po.Status) : query.OrderBy(po => po.Status),
            "amount" => request.Pagination.SortDescending ? query.OrderByDescending(po => po.TotalAmount) : query.OrderBy(po => po.TotalAmount),
            _ => query.OrderByDescending(po => po.OrderDate)
        };

        var projectedQuery = query.Select(po => new PurchaseOrderDto(
            po.Id,
            po.OrderNumber,
            po.Supplier.Name,
            po.Warehouse.Name,
            po.OrderDate,
            po.ExpectedDeliveryDate,
            po.Status.ToString(),
            po.TotalAmount,
            po.Items.Select(i => new PurchaseOrderItemDto(
                i.Id,
                i.Product.Name,
                i.Product.Sku,
                i.Quantity,
                i.ReceivedQuantity,
                i.UnitPrice,
                i.LineTotal)).ToList()));

        var result = await PaginatedList<PurchaseOrderDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<PurchaseOrderDto>>.Success(result);
    }
}
