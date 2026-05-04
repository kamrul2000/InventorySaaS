using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _context;

    public ReportService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaginatedList<StockSummaryReportDto>> GetStockSummaryAsync(
        PaginationParams pagination,
        Guid? warehouseId,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .AsNoTracking()
            .Include(ib => ib.Product).ThenInclude(p => p.Category)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand > 0);

        if (warehouseId.HasValue)
            query = query.Where(ib => ib.WarehouseId == warehouseId.Value);

        if (categoryId.HasValue)
            query = query.Where(ib => ib.Product.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        var orderedQuery = pagination.SortBy?.ToLowerInvariant() switch
        {
            "value" => pagination.SortDescending
                ? query.OrderByDescending(ib => ib.QuantityOnHand * ib.UnitCost)
                : query.OrderBy(ib => ib.QuantityOnHand * ib.UnitCost),
            "quantity" => pagination.SortDescending
                ? query.OrderByDescending(ib => ib.QuantityOnHand)
                : query.OrderBy(ib => ib.QuantityOnHand),
            _ => query.OrderBy(ib => ib.Product.Name)
        };

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(ib => new StockSummaryReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Product.Category?.Name ?? "Uncategorized",
            ib.Warehouse.Name,
            ib.QuantityOnHand,
            ib.UnitCost,
            ib.QuantityOnHand * ib.UnitCost)).ToList();

        return new PaginatedList<StockSummaryReportDto>(
            dtos, totalCount, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<PaginatedList<LowStockReportDto>> GetLowStockAsync(
        PaginationParams pagination,
        CancellationToken cancellationToken)
    {
        var query = _context.InventoryBalances
            .AsNoTracking()
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand <= ib.Product.ReorderLevel && ib.QuantityOnHand > 0);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

        var orderedQuery = query.OrderByDescending(ib => ib.Product.ReorderLevel - ib.QuantityOnHand);

        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        var items = await orderedQuery
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(ib => new LowStockReportDto(
            ib.Product.Name,
            ib.Product.Sku,
            ib.Warehouse.Name,
            ib.QuantityOnHand,
            ib.Product.ReorderLevel,
            ib.Product.ReorderLevel - ib.QuantityOnHand)).ToList();

        return new PaginatedList<LowStockReportDto>(
            dtos, totalCount, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<PaginatedList<ExpiryReportDto>> GetExpiryAsync(
        PaginationParams pagination,
        int daysAhead,
        CancellationToken cancellationToken)
    {
        var expiryThreshold = DateTime.UtcNow.AddDays(daysAhead);
        var now = DateTime.UtcNow;

        var query = _context.InventoryBalances
            .AsNoTracking()
            .Where(ib =>
                ib.ExpiryDate != null &&
                ib.ExpiryDate <= expiryThreshold &&
                ib.QuantityOnHand > 0);

        if (!string.IsNullOrWhiteSpace(pagination.SearchTerm))
        {
            var searchTerm = pagination.SearchTerm.ToLowerInvariant();
            query = query.Where(ib =>
                ib.Product.Name.ToLower().Contains(searchTerm) ||
                ib.Product.Sku.ToLower().Contains(searchTerm));
        }

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
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(i => new ExpiryReportDto(
            i.Name, i.Sku, i.WarehouseName, i.BatchNumber,
            i.ExpiryDate, i.QuantityOnHand,
            (int)(i.ExpiryDate - now).TotalDays)).ToList();

        return new PaginatedList<ExpiryReportDto>(
            dtos, totalCount, pagination.PageNumber, pagination.PageSize);
    }

    public async Task<List<InventoryValuationDto>> GetInventoryValuationAsync(CancellationToken cancellationToken)
    {
        var balances = await _context.InventoryBalances
            .AsNoTracking()
            .Include(ib => ib.Product).ThenInclude(p => p.Category)
            .Where(ib => ib.QuantityOnHand > 0)
            .ToListAsync(cancellationToken);

        return balances
            .GroupBy(ib => ib.Product.Category?.Name ?? "Uncategorized")
            .Select(g => new InventoryValuationDto(
                g.Key,
                g.Select(ib => ib.ProductId).Distinct().Count(),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.UnitCost),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.Product.SellingPrice)))
            .OrderByDescending(v => v.TotalCostValue)
            .ToList();
    }
}
