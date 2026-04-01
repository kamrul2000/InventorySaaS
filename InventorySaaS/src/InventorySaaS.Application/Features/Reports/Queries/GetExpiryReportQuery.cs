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
                ib.QuantityOnHand > 0)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        var projectedQuery = query.Select(ib => new ExpiryReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Warehouse.Name,
            ib.BatchNumber,
            ib.ExpiryDate!.Value,
            ib.QuantityOnHand,
            EF.Functions.DateDiffDay(now, ib.ExpiryDate!.Value)));

        projectedQuery = projectedQuery.OrderBy(x => x.DaysUntilExpiry);

        var result = await PaginatedList<ExpiryReportDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<ExpiryReportDto>>.Success(result);
    }
}
