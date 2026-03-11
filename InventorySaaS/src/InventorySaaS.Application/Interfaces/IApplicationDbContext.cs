using InventorySaaS.Domain.Entities.Audit;
using InventorySaaS.Domain.Entities.Customer;
using InventorySaaS.Domain.Entities.Identity;
using InventorySaaS.Domain.Entities.Inventory;
using InventorySaaS.Domain.Entities.Notification;
using InventorySaaS.Domain.Entities.Product;
using InventorySaaS.Domain.Entities.Purchase;
using InventorySaaS.Domain.Entities.Sales;
using InventorySaaS.Domain.Entities.Supplier;
using InventorySaaS.Domain.Entities.Tenant;
using InventorySaaS.Domain.Entities.Warehouse;
using Microsoft.EntityFrameworkCore;

namespace InventorySaaS.Application.Interfaces;

public interface IApplicationDbContext
{
    // Tenant
    DbSet<TenantInfo> Tenants { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }

    // Identity
    DbSet<ApplicationUser> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    // Product
    DbSet<ProductInfo> Products { get; }
    DbSet<Category> Categories { get; }
    DbSet<Brand> Brands { get; }
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<ProductImage> ProductImages { get; }

    // Warehouse
    DbSet<WarehouseInfo> Warehouses { get; }
    DbSet<WarehouseLocation> WarehouseLocations { get; }

    // Inventory
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }

    // Supplier & Customer
    DbSet<SupplierInfo> Suppliers { get; }
    DbSet<CustomerInfo> Customers { get; }

    // Purchase
    DbSet<PurchaseRequisition> PurchaseRequisitions { get; }
    DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptItem> GoodsReceiptItems { get; }

    // Sales
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderItem> SalesOrderItems { get; }

    // Notification & Audit
    DbSet<NotificationInfo> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
