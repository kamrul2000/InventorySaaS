using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.SalesOrders.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.SalesOrders.Queries;

public record GetSalesOrdersQuery(PaginationParams Pagination) : IRequest<Result<PaginatedList<SalesOrderDto>>>;

public class GetSalesOrdersQueryHandler : IRequestHandler<GetSalesOrdersQuery, Result<PaginatedList<SalesOrderDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetSalesOrdersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<SalesOrderDto>>> Handle(GetSalesOrdersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.SalesOrders
            .Include(so => so.Customer)
            .Include(so => so.Warehouse)
            .Include(so => so.Items)
                .ThenInclude(i => i.Product)
            .Where(so => !so.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(so =>
                so.OrderNumber.ToLower().Contains(searchTerm) ||
                so.Customer.Name.ToLower().Contains(searchTerm));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "ordernumber" => request.Pagination.SortDescending ? query.OrderByDescending(so => so.OrderNumber) : query.OrderBy(so => so.OrderNumber),
            "customer" => request.Pagination.SortDescending ? query.OrderByDescending(so => so.Customer.Name) : query.OrderBy(so => so.Customer.Name),
            "status" => request.Pagination.SortDescending ? query.OrderByDescending(so => so.Status) : query.OrderBy(so => so.Status),
            "amount" => request.Pagination.SortDescending ? query.OrderByDescending(so => so.TotalAmount) : query.OrderBy(so => so.TotalAmount),
            _ => query.OrderByDescending(so => so.OrderDate)
        };

        var projectedQuery = query.Select(so => new SalesOrderDto(
            so.Id,
            so.OrderNumber,
            so.Customer.Name,
            so.Warehouse.Name,
            so.OrderDate,
            so.DeliveryDate,
            so.Status.ToString(),
            so.TotalAmount,
            so.Items.Select(i => new SalesOrderItemDto(
                i.Id,
                i.ProductId,
                i.Product.Name,
                i.Product.Sku,
                i.Quantity,
                i.DeliveredQuantity,
                i.ReturnedQuantity,
                i.UnitPrice,
                i.LineTotal)).ToList()));

        var result = await PaginatedList<SalesOrderDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<SalesOrderDto>>.Success(result);
    }
}
