using InventorySaaS.Application.Features.Dashboard.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;

    public DashboardService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken)
    {
        var totalProducts = await _context.Products.CountAsync(cancellationToken);
        var totalWarehouses = await _context.Warehouses.CountAsync(cancellationToken);
        var totalSuppliers = await _context.Suppliers.CountAsync(cancellationToken);
        var totalCustomers = await _context.Customers.CountAsync(cancellationToken);

        var lowStockCount = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0 && ib.QuantityOnHand <= ib.Product.ReorderLevel)
            .Select(ib => ib.ProductId)
            .Distinct()
            .CountAsync(cancellationToken);

        var expiryThreshold = DateTime.UtcNow.AddDays(30);
        var expiringCount = await _context.InventoryBalances
            .Where(ib => ib.ExpiryDate != null && ib.ExpiryDate <= expiryThreshold && ib.QuantityOnHand > 0)
            .CountAsync(cancellationToken);

        var balances = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0)
            .Select(ib => new { ib.QuantityOnHand, ib.UnitCost })
            .ToListAsync(cancellationToken);
        var totalInventoryValue = balances.Sum(b => (decimal)b.QuantityOnHand * b.UnitCost);

        var salesOrders = await _context.SalesOrders
            .Where(so => so.Status != SalesOrderStatus.Draft && so.Status != SalesOrderStatus.Cancelled)
            .Select(so => so.TotalAmount)
            .ToListAsync(cancellationToken);
        var totalSales = salesOrders.Sum();

        var purchaseOrders = await _context.PurchaseOrders
            .Where(po => po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Cancelled)
            .Select(po => po.TotalAmount)
            .ToListAsync(cancellationToken);
        var totalPurchases = purchaseOrders.Sum();

        var totalOrders = await _context.SalesOrders
            .Where(so => so.Status != SalesOrderStatus.Draft && so.Status != SalesOrderStatus.Cancelled)
            .CountAsync(cancellationToken);

        var recentTransactions = await _context.InventoryTransactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(10)
            .Select(t => new RecentTransactionDto(
                t.TransactionNumber,
                t.TransactionType.ToString(),
                t.Product.Name,
                t.Quantity,
                t.TransactionDate))
            .ToListAsync(cancellationToken);

        var allBalances = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0)
            .Select(ib => new { ib.Product.Name, ib.Product.Sku, ib.Product.SellingPrice, ib.QuantityOnHand, ib.UnitCost })
            .ToListAsync(cancellationToken);

        var topProducts = allBalances
            .GroupBy(ib => new { ib.Name, ib.Sku, ib.SellingPrice })
            .Select(g => new TopProductDto(
                g.Key.Name,
                g.Key.Sku,
                g.Sum(ib => ib.QuantityOnHand),
                g.Sum(ib => (decimal)ib.QuantityOnHand * ib.UnitCost),
                g.Key.SellingPrice))
            .OrderByDescending(x => x.TotalQuantity)
            .Take(5)
            .ToList();

        var stockAlerts = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0 && ib.QuantityOnHand <= ib.Product.ReorderLevel)
            .OrderBy(ib => ib.QuantityOnHand)
            .Take(10)
            .Select(ib => new StockAlertDto(
                ib.Product.Name,
                ib.Product.Sku,
                ib.Warehouse.Name,
                ib.QuantityOnHand,
                ib.Product.ReorderLevel))
            .ToListAsync(cancellationToken);

        var recentSalesOrders = await _context.SalesOrders
            .OrderByDescending(so => so.OrderDate)
            .Take(5)
            .Select(so => new RecentSalesOrderDto(
                so.OrderNumber,
                so.Customer.Name,
                so.Status.ToString(),
                so.TotalAmount,
                so.OrderDate))
            .ToListAsync(cancellationToken);

        var lowStockRaw = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0 && ib.QuantityOnHand <= ib.Product.ReorderLevel)
            .Select(ib => new { ib.Product.Name, ib.Product.Sku, ib.QuantityOnHand, ib.Product.ReorderLevel })
            .ToListAsync(cancellationToken);

        var lowStockProducts = lowStockRaw
            .GroupBy(x => new { x.Name, x.Sku })
            .Select(g => new LowStockProductDto(
                g.Key.Name,
                g.Key.Sku,
                g.Sum(x => x.QuantityOnHand),
                g.Max(x => x.ReorderLevel)))
            .OrderBy(x => x.CurrentStock)
            .Take(5)
            .ToList();

        return new DashboardDto(
            totalProducts,
            totalWarehouses,
            totalSuppliers,
            totalCustomers,
            lowStockCount,
            expiringCount,
            totalInventoryValue,
            totalSales,
            totalPurchases,
            totalOrders,
            recentTransactions,
            topProducts,
            stockAlerts,
            recentSalesOrders,
            lowStockProducts);
    }
}
