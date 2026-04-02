using InventorySaaS.Application.Common.Models;
using InventorySaaS.Application.Features.Dashboard.DTOs;
using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Features.Dashboard.Queries;

public record GetDashboardQuery : IRequest<Result<DashboardDto>>;

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, Result<DashboardDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDashboardQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var totalProducts = await _context.Products
            .CountAsync(cancellationToken);

        var totalWarehouses = await _context.Warehouses
            .CountAsync(cancellationToken);

        var totalSuppliers = await _context.Suppliers
            .CountAsync(cancellationToken);

        var totalCustomers = await _context.Customers
            .CountAsync(cancellationToken);

        // Low stock count
        var lowStockCount = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0 && ib.QuantityOnHand <= ib.Product.ReorderLevel)
            .Select(ib => ib.ProductId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Expiring within 30 days
        var expiryThreshold = DateTime.UtcNow.AddDays(30);
        var expiringCount = await _context.InventoryBalances
            .Where(ib => ib.ExpiryDate != null && ib.ExpiryDate <= expiryThreshold && ib.QuantityOnHand > 0)
            .CountAsync(cancellationToken);

        // Total inventory value
        var balances = await _context.InventoryBalances
            .Where(ib => ib.QuantityOnHand > 0)
            .Select(ib => new { ib.QuantityOnHand, ib.UnitCost })
            .ToListAsync(cancellationToken);
        var totalInventoryValue = balances.Sum(b => (decimal)b.QuantityOnHand * b.UnitCost);

        // Total sales (non-draft, non-cancelled)
        var salesOrders = await _context.SalesOrders
            .Where(so => so.Status != SalesOrderStatus.Draft && so.Status != SalesOrderStatus.Cancelled)
            .Select(so => so.TotalAmount)
            .ToListAsync(cancellationToken);
        var totalSales = salesOrders.Sum();

        // Total purchases (non-draft, non-cancelled)
        var purchaseOrders = await _context.PurchaseOrders
            .Where(po => po.Status != PurchaseOrderStatus.Draft && po.Status != PurchaseOrderStatus.Cancelled)
            .Select(po => po.TotalAmount)
            .ToListAsync(cancellationToken);
        var totalPurchases = purchaseOrders.Sum();

        // Total orders count (sales)
        var totalOrders = await _context.SalesOrders
            .Where(so => so.Status != SalesOrderStatus.Draft && so.Status != SalesOrderStatus.Cancelled)
            .CountAsync(cancellationToken);

        // Recent transactions (last 10)
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

        // Top products by total on-hand quantity
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

        // Stock alerts
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

        // Recent sales orders (last 5)
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

        // Low stock products (distinct, top 5)
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

        var dashboard = new DashboardDto(
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

        return Result<DashboardDto>.Success(dashboard);
    }
}
