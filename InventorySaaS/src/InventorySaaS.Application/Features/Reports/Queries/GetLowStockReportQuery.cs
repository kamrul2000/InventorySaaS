using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Reports.Queries;

public record GetLowStockReportQuery(PaginationParams Pagination) : IRequest<Result<PaginatedList<LowStockReportDto>>>;

public class GetLowStockReportQueryHandler : IRequestHandler<GetLowStockReportQuery, Result<PaginatedList<LowStockReportDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetLowStockReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<LowStockReportDto>>> Handle(GetLowStockReportQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand <= ib.Product.ReorderLevel && ib.QuantityOnHand > 0)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        var projectedQuery = query.Select(ib => new LowStockReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Warehouse.Name,
            ib.QuantityOnHand,
            ib.Product.ReorderLevel,
            ib.Product.ReorderLevel - ib.QuantityOnHand));

        projectedQuery = projectedQuery.OrderByDescending(x => x.Deficit);

        var result = await PaginatedList<LowStockReportDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<LowStockReportDto>>.Success(result);
    }
}
