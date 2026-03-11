using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Inventory.DTOs;
using InventorySaaS.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Inventory.Queries;

public record GetInventoryTransactionsQuery(
    PaginationParams Pagination,
    Guid? WarehouseId = null,
    Guid? ProductId = null) : IRequest<Result<PaginatedList<InventoryTransactionDto>>>;

public class GetInventoryTransactionsQueryHandler : IRequestHandler<GetInventoryTransactionsQuery, Result<PaginatedList<InventoryTransactionDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetInventoryTransactionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedList<InventoryTransactionDto>>> Handle(GetInventoryTransactionsQuery request, CancellationToken cancellationToken)
    {
        var query = _context.InventoryTransactions
            .Include(t => t.Product)
            .Include(t => t.Warehouse)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(t => t.WarehouseId == request.WarehouseId.Value);

        if (request.ProductId.HasValue)
            query = query.Where(t => t.ProductId == request.ProductId.Value);

        if (!string.IsNullOrWhiteSpace(request.Pagination.SearchTerm))
        {
            var searchTerm = request.Pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(t =>
                t.TransactionNumber.ToLower().Contains(searchTerm) ||
                t.Product.Name.ToLower().Contains(searchTerm) ||
                t.Product.Sku.ToLower().Contains(searchTerm));
        }

        query = request.Pagination.SortBy?.ToLowerInvariant() switch
        {
            "date" => request.Pagination.SortDescending ? query.OrderByDescending(t => t.TransactionDate) : query.OrderBy(t => t.TransactionDate),
            "product" => request.Pagination.SortDescending ? query.OrderByDescending(t => t.Product.Name) : query.OrderBy(t => t.Product.Name),
            _ => query.OrderByDescending(t => t.TransactionDate)
        };

        var projectedQuery = query.Select(t => new InventoryTransactionDto(
            t.Id,
            t.TransactionNumber,
            t.TransactionType.ToString(),
            t.Product.Name,
            t.Product.Sku,
            t.Warehouse.Name,
            t.Quantity,
            t.UnitCost,
            t.BatchNumber,
            t.TransactionDate,
            t.Notes));

        var result = await PaginatedList<InventoryTransactionDto>.CreateAsync(
            projectedQuery,
            request.Pagination.PageNumber,
            request.Pagination.PageSize,
            cancellationToken);

        return Result<PaginatedList<InventoryTransactionDto>>.Success(result);
    }
}
