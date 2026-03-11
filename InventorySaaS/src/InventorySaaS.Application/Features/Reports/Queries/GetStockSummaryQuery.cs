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
            .Include(ib => ib.Product)
                .ThenInclude(p => p.Category)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand > 0)
            .AsQueryable();

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

        var projectedQuery = query.Select(ib => new StockSummaryReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Product.Category.Name,
            ib.Warehouse.Name,
            ib.QuantityOnHand,
            ib.UnitCost,
            ib.QuantityOnHand * ib.UnitCost));

        projectedQuery = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "product" => request.Pagination.SortDescending ? projectedQuery.OrderByDescending(x => x.ProductName) : projectedQuery.OrderBy(x => x.ProductName),
            "value" => request.Pagination.SortDescending ? projectedQuery.OrderByDescending(x => x.TotalValue) : projectedQuery.OrderBy(x => x.TotalValue),
            "quantity" => request.Pagination.SortDescending ? projectedQuery.OrderByDescending(x => x.QuantityOnHand) : projectedQuery.OrderBy(x => x.QuantityOnHand),
            _ => projectedQuery.OrderBy(x => x.ProductName)
        };

        var result = await PaginatedList<StockSummaryReportDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<StockSummaryReportDto>>.Success(result);
    }
}
