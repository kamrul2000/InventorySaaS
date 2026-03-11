using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Warehouses.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Warehouses.Queries;

public record GetWarehousesQuery(PaginationParams Pagination) : IRequest<Result<PaginatedList<WarehouseDto>>>;

public class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, Result<PaginatedList<WarehouseDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetWarehousesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<WarehouseDto>>> Handle(GetWarehousesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Warehouses
            .Include(w => w.Locations)
            .Where(w => !w.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(w =>
                w.Name.ToLower().Contains(searchTerm) ||
                w.Code.ToLower().Contains(searchTerm));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "name" => request.Pagination.SortDescending ? query.OrderByDescending(w => w.Name) : query.OrderBy(w => w.Name),
            "code" => request.Pagination.SortDescending ? query.OrderByDescending(w => w.Code) : query.OrderBy(w => w.Code),
            _ => query.OrderBy(w => w.Name)
        };

        var projectedQuery = query.Select(w => new WarehouseDto(
            w.Id,
            w.Name,
            w.Code,
            w.Address,
            w.City,
            w.IsDefault,
            w.IsActive,
            w.Locations.Count));

        var result = await PaginatedList<WarehouseDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<WarehouseDto>>.Success(result);
    }
}
