using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Inventory.Queries;

public record GetInventoryBalancesQuery(
    PaginationParams Pagination,
    Guid? WarehouseId = null,
    Guid? ProductId = null) : IRequest<Result<PaginatedList<InventoryBalanceDto>>>;

public class GetInventoryBalancesQueryHandler : IRequestHandler<GetInventoryBalancesQuery, Result<PaginatedList<InventoryBalanceDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryBalancesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<InventoryBalanceDto>>> Handle(GetInventoryBalancesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Include(ib => ib.Location)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(ib => ib.WarehouseId == request.WarehouseId.Value);

        if (request.ProductId.HasValue)
            query = query.Where(ib => ib.ProductId == request.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "product" => request.Pagination.SortDescending ? query.OrderByDescending(ib => ib.Product.Name) : query.OrderBy(ib => ib.Product.Name),
            "warehouse" => request.Pagination.SortDescending ? query.OrderByDescending(ib => ib.Warehouse.Name) : query.OrderBy(ib => ib.Warehouse.Name),
            "quantity" => request.Pagination.SortDescending ? query.OrderByDescending(ib => ib.QuantityOnHand) : query.OrderBy(ib => ib.QuantityOnHand),
            _ => query.OrderBy(ib => ib.Product.Name)
        };

        var projectedQuery = query.Select(ib => new InventoryBalanceDto(
            ib.Id,
            ib.ProductId,
            ib.Product.Name,
            ib.Product.Sku,
            ib.WarehouseId,
            ib.Warehouse.Name,
            ib.Location != null ? ib.Location.Name : null,
            ib.BatchNumber,
            ib.ExpiryDate,
            ib.QuantityOnHand,
            ib.QuantityReserved,
            ib.QuantityOnHand - ib.QuantityReserved,
            ib.UnitCost));

        var result = await PaginatedList<InventoryBalanceDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<InventoryBalanceDto>>.Success(result);
    }
}
