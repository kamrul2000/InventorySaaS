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
            .AsNoTracking()
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand <= ib.Product.ReorderLevel && ib.QuantityOnHand > 0);

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        var orderedQuery = query.OrderByDescending(ib => ib.Product.ReorderLevel - ib.QuantityOnHand);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(ib => new LowStockReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Warehouse.Name,
            ib.QuantityOnHand,
            ib.Product.ReorderLevel,
            ib.Product.ReorderLevel - ib.QuantityOnHand)).ToList();

        var result = new PaginatedList<LowStockReportDto>(
            dtos, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);

        return Result<PaginatedList<LowStockReportDto>>.Success(result);
    }
}
