using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Reports.Queries;

public record GetExpiryReportQuery(PaginationParams Pagination, int DaysAhead = 30) : IRequest<Result<PaginatedList<ExpiryReportDto>>>;

public class GetExpiryReportQueryHandler : IRequestHandler<GetExpiryReportQuery, Result<PaginatedList<ExpiryReportDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetExpiryReportQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<ExpiryReportDto>>> Handle(GetExpiryReportQuery request, CancellationToken cancellationToken)
    {
        var expiryThreshold = DateTime.UtcNow.AddDays(request.DaysAhead);
        var now = DateTime.UtcNow;

        var query = _context.InventoryBalances
            .AsNoTracking()
            .Where(ib =>
                ib.ExpiryDate != null &&
                ib.ExpiryDate <= expiryThreshold &&
                ib.QuantityOnHand > 0);

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        // Project DB columns only (no date math in SQL)
        var dbQuery = query
            .OrderBy(ib => ib.ExpiryDate)
            .Select(ib => new
            {
                ib.Product.Name,
                ib.Product.Sku,
                WarehouseName = ib.Warehouse.Name,
                ib.BatchNumber,
                ExpiryDate = ib.ExpiryDate!.Value,
                ib.QuantityOnHand,
            });

        var totalCount = await dbQuery.CountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        // Compute DaysUntilExpiry in memory
        var dtos = items.Select(i => new ExpiryReportDto(
            i.Name, i.Sku, i.WarehouseName, i.BatchNumber,
            i.ExpiryDate, i.QuantityOnHand,
            (int)(i.ExpiryDate - now).TotalDays)).ToList();

        var result = new PaginatedList<ExpiryReportDto>(
            dtos, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);

        return Result<PaginatedList<ExpiryReportDto>>.Success(result);
    }
}
