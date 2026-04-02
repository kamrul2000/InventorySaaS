using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Reports.Queries;

public record GetStockSummaryQuery(
    PaginationParams Pagination,
    Guid? WarehouseId = null,
    Guid? CategoryId = null) : IRequest<Result<PaginatedList<StockSummaryReportDto>>>;

public class GetStockSummaryQueryHandler : IRequestHandler<GetStockSummaryQuery, Result<PaginatedList<StockSummaryReportDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetStockSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<StockSummaryReportDto>>> Handle(GetStockSummaryQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .AsNoTracking()
            .Include(ib => ib.Product).ThenInclude(p => p.Category)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand > 0);

        if (request.WarehouseId.HasValue)
            query = query.Where(ib => ib.WarehouseId == request.WarehouseId.Value);

        if (request.CategoryId.HasValue)
            query = query.Where(ib => ib.Product.CategoryId == request.CategoryId.Value);

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        var orderedQuery = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "value" => request.Pagination.SortDescending
                ? query.OrderByDescending(ib => ib.QuantityOnHand * ib.UnitCost)
                : query.OrderBy(ib => ib.QuantityOnHand * ib.UnitCost),
            "quantity" => request.Pagination.SortDescending
                ? query.OrderByDescending(ib => ib.QuantityOnHand)
                : query.OrderBy(ib => ib.QuantityOnHand),
            _ => query.OrderBy(ib => ib.Product.Name)
        };

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(ib => new StockSummaryReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Product.Category?.Name ?? "Uncategorized",
            ib.Warehouse.Name,
            ib.QuantityOnHand,
            ib.UnitCost,
            ib.QuantityOnHand * ib.UnitCost)).ToList();

        var result = new PaginatedList<StockSummaryReportDto>(
            dtos, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);

        return Result<PaginatedList<StockSummaryReportDto>>.Success(result);
    }
}
