using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Reports.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
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
    public async Task<List<AgingReportDto>> GetArAgingAsync(
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        // BalanceDue is a computed property, so the outstanding test has to be written out in SQL terms.
        var invoices = await _context.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.Status != InvoiceStatus.Draft
                     && i.Status != InvoiceStatus.Cancelled
                     && i.TotalAmount - i.AmountPaid > 0)
            .Select(i => new OutstandingDocument(
                i.Customer.Name, i.DueDate, i.TotalAmount - i.AmountPaid))
            .ToListAsync(cancellationToken);

        return BuildAging(invoices, asOf);
    }

    public async Task<List<AgingReportDto>> GetApAgingAsync(
        DateTime asOf,
        CancellationToken cancellationToken)
    {
        var bills = await _context.SupplierBills
            .AsNoTracking()
            .Include(b => b.Supplier)
            .Where(b => b.Status != BillStatus.Draft
                     && b.Status != BillStatus.Cancelled
                     && b.TotalAmount - b.AmountPaid > 0)
            .Select(b => new OutstandingDocument(
                b.Supplier.Name, b.DueDate, b.TotalAmount - b.AmountPaid))
            .ToListAsync(cancellationToken);

        return BuildAging(bills, asOf);
    }

    /// <summary>
    /// Receivables and payables age identically, so both sides share one bucketing pass.
    /// A document due today or later is Current; everything else falls in a 30-day band.
    /// </summary>
    private static List<AgingReportDto> BuildAging(List<OutstandingDocument> documents, DateTime asOf)
    {
        return documents
            .GroupBy(d => d.PartyName)
            .Select(g =>
            {
                decimal current = 0, d1 = 0, d31 = 0, d61 = 0, d90 = 0;
                var oldest = 0;

                foreach (var doc in g)
                {
                    var daysOverdue = (asOf.Date - doc.DueDate.Date).Days;
                    if (daysOverdue > oldest) oldest = daysOverdue;

                    if (daysOverdue <= 0) current += doc.BalanceDue;
                    else if (daysOverdue <= 30) d1 += doc.BalanceDue;
                    else if (daysOverdue <= 60) d31 += doc.BalanceDue;
                    else if (daysOverdue <= 90) d61 += doc.BalanceDue;
                    else d90 += doc.BalanceDue;
                }

                return new AgingReportDto(
                    g.Key, current, d1, d31, d61, d90,
                    current + d1 + d31 + d61 + d90,
                    g.Count(),
                    oldest < 0 ? 0 : oldest);
            })
            .OrderByDescending(a => a.Total)
            .ToList();
    }

    public async Task<List<SalesSummaryDto>> GetSalesSummaryAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        // Draft orders are not sales yet and cancelled ones never were.
        var query = _context.SalesOrders
            .AsNoTracking()
            .Include(so => so.Customer)
            .Include(so => so.Items)
            .Where(so => so.Status != SalesOrderStatus.Draft && so.Status != SalesOrderStatus.Cancelled);

        if (startDate.HasValue) query = query.Where(so => so.OrderDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(so => so.OrderDate <= endDate.Value);

        var orders = await query.ToListAsync(cancellationToken);

        return orders
            .GroupBy(so => so.Customer.Name)
            .Select(g => new SalesSummaryDto(
                g.Key,
                g.Count(),
                g.Sum(so => so.Items.Sum(i => i.Quantity)),
                g.Sum(so => so.SubTotal),
                g.Sum(so => so.TaxAmount),
                g.Sum(so => so.DiscountAmount),
                g.Sum(so => so.TotalAmount)))
            .OrderByDescending(s => s.TotalAmount)
            .ToList();
    }

    public async Task<List<PurchaseSummaryDto>> GetPurchaseSummaryAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var query = _context.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Supplier)
            .Include(po => po.Items)
            .Where(po => po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Cancelled);

        if (startDate.HasValue) query = query.Where(po => po.OrderDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(po => po.OrderDate <= endDate.Value);

        var orders = await query.ToListAsync(cancellationToken);

        return orders
            .GroupBy(po => po.Supplier.Name)
            .Select(g => new PurchaseSummaryDto(
                g.Key,
                g.Count(),
                g.Sum(po => po.Items.Sum(i => i.Quantity)),
                g.Sum(po => po.Items.Sum(i => i.ReceivedQuantity)),
                g.Sum(po => po.TotalAmount)))
            .OrderByDescending(p => p.TotalAmount)
            .ToList();
    }

    public async Task<List<ProfitabilityDto>> GetProfitabilityAsync(
        DateTime? startDate,
        DateTime? endDate,
        CancellationToken cancellationToken)
    {
        // Delivery writes the cost of the stock it actually drew down onto the ledger, so the
        // ledger — not the catalogue CostPrice — is the honest source for COGS.
        var query = _context.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.Product).ThenInclude(p => p.Category)
            .Where(t => t.ReferenceType == "SalesOrder"
                     && (t.TransactionType == TransactionType.SalesIssue
                      || t.TransactionType == TransactionType.Return));

        if (startDate.HasValue) query = query.Where(t => t.TransactionDate >= startDate.Value);
        if (endDate.HasValue) query = query.Where(t => t.TransactionDate <= endDate.Value);

        var movements = await query.ToListAsync(cancellationToken);
        if (movements.Count == 0) return [];

        // Selling price lives on the order line, so pull the lines these movements point at.
        var orderIds = movements
            .Where(t => t.ReferenceId.HasValue)
            .Select(t => t.ReferenceId!.Value)
            .Distinct()
            .ToList();

        var lines = await _context.SalesOrderItems
            .AsNoTracking()
            .Where(i => orderIds.Contains(i.SalesOrderId))
            .Select(i => new { i.SalesOrderId, i.ProductId, i.UnitPrice, i.DiscountRate })
            .ToListAsync(cancellationToken);

        // Net of discount but before tax: tax is collected on behalf of the state, not margin.
        var netUnitPrice = lines
            .GroupBy(l => (l.SalesOrderId, l.ProductId))
            .ToDictionary(
                g => g.Key,
                g => g.First().UnitPrice * (1 - g.First().DiscountRate / 100m));

        return movements
            .GroupBy(t => t.ProductId)
            .Select(g =>
            {
                var product = g.First().Product;
                var units = 0;
                decimal revenue = 0, cost = 0;

                foreach (var movement in g)
                {
                    // A return reverses the sale it came from: negative units, revenue and cost.
                    var sign = movement.TransactionType == TransactionType.Return ? -1 : 1;
                    var price = movement.ReferenceId.HasValue
                        && netUnitPrice.TryGetValue((movement.ReferenceId.Value, movement.ProductId), out var p)
                        ? p
                        : 0m;

                    units += sign * movement.Quantity;
                    revenue += sign * movement.Quantity * price;
                    cost += sign * movement.Quantity * movement.UnitCost;
                }

                var profit = revenue - cost;

                return new ProfitabilityDto(
                    product.Name,
                    product.Sku,
                    product.Category?.Name ?? "Uncategorized",
                    units,
                    revenue,
                    cost,
                    profit,
                    revenue == 0 ? 0 : Math.Round(profit / revenue * 100m, 2));
            })
            .OrderByDescending(p => p.GrossProfit)
            .ToList();
    }

    /// <summary>An unpaid invoice or bill reduced to just what the aging calculation needs.</summary>
    private sealed record OutstandingDocument(string PartyName, DateTime DueDate, decimal BalanceDue);
}