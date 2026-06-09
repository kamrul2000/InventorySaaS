using InventorySaaS.Application.Interfaces;
using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Interfaces;
using InventorySaaS.Domain.Entities.Audit;
using InventorySaaS.Domain.Entities.Billing;
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
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.Text.Json;

namespace InventorySaaS.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ICurrentUserService _currentUserService;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ITenantAccessor tenantAccessor,
        ICurrentUserService currentUserService) : base(options)
    {
        _tenantAccessor = tenantAccessor;
        _currentUserService = currentUserService;
    }

    // Tenant
    public DbSet<TenantInfo> Tenants => Set<TenantInfo>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

    // Identity
    public DbSet<ApplicationUser> Users => Set<ApplicationUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    // Product
    public DbSet<ProductInfo> Products => Set<ProductInfo>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    // Warehouse
    public DbSet<WarehouseInfo> Warehouses => Set<WarehouseInfo>();
    public DbSet<WarehouseLocation> WarehouseLocations => Set<WarehouseLocation>();

    // Inventory
    public DbSet<InventoryBalance> InventoryBalances => Set<InventoryBalance>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // Supplier & Customer
    public DbSet<SupplierInfo> Suppliers => Set<SupplierInfo>();
    public DbSet<CustomerInfo> Customers => Set<CustomerInfo>();

    // Purchase
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionItem> PurchaseRequisitionItems => Set<PurchaseRequisitionItem>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();

    // Sales
    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();
    public DbSet<SalesOrderItem> SalesOrderItems => Set<SalesOrderItem>();

    // Billing (Accounts Receivable)
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    // Notification & Audit
    public DbSet<NotificationInfo> Notifications => Set<NotificationInfo>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Global query filters. EF Core allows only ONE query filter per entity, so the
        // tenant-isolation and soft-delete predicates must be combined into a single filter.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(TenantEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetTenantAndSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder]);
            }
            else if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(SetSoftDeleteFilter), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(null, [modelBuilder]);
            }
        }
    }

    // Instance method so the filter can reference the (per-request) resolved tenant.
    // A null tenant (system operations: seeding, registration, SuperAdmin without a tenant
    // context) bypasses tenant scoping; authenticated tenant users always carry a tenant id.
    private void SetTenantAndSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : TenantEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e =>
            (_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId) && !e.IsDeleted);
    }

    private static void SetSoftDeleteFilter<T>(ModelBuilder modelBuilder) where T : BaseEntity
    {
        modelBuilder.Entity<T>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantAccessor.TenantId;
        var userId = _currentUserService.UserId?.ToString();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.CreatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        // Auto-set TenantId on new tenant entities
        if (tenantId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries<TenantEntity>())
            {
                if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
                {
                    entry.Entity.TenantId = tenantId.Value;
                }
            }
        }

        // Capture change history before saving (original values are reset after SaveChanges).
        var auditEntries = CollectAuditEntries(tenantId);

        var result = await base.SaveChangesAsync(cancellationToken);

        if (auditEntries.Count > 0)
        {
            AuditLogs.AddRange(auditEntries);
            await base.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private List<AuditLog> CollectAuditEntries(Guid? tenantId)
    {
        var entries = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            // A soft-delete surfaces as a Modified entry with IsDeleted flipped to true.
            var action = entry.State switch
            {
                EntityState.Added => "Create",
                EntityState.Deleted => "Delete",
                _ => entry.Entity.IsDeleted ? "Delete" : "Update"
            };

            string? oldValues = null;
            string? newValues = null;

            if (entry.State == EntityState.Modified)
            {
                var changed = entry.Properties
                    .Where(p => p.IsModified && p.Metadata.Name != nameof(BaseEntity.RowVersion))
                    .ToList();
                oldValues = Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                newValues = Serialize(changed.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
            }
            else if (entry.State == EntityState.Added)
            {
                newValues = Serialize(ScalarValues(entry));
            }
            else
            {
                oldValues = Serialize(ScalarValues(entry));
            }

            entries.Add(new AuditLog
            {
                TenantId = tenantId,
                UserId = _currentUserService.UserId,
                UserEmail = _currentUserService.Email,
                Action = action,
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Entity.Id.ToString(),
                OldValues = oldValues,
                NewValues = newValues,
                Timestamp = DateTime.UtcNow
            });
        }

        return entries;
    }

    private static Dictionary<string, object?> ScalarValues(EntityEntry entry) =>
        entry.Properties
            .Where(p => p.Metadata.Name != nameof(BaseEntity.RowVersion))
            .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);

    private static string Serialize(Dictionary<string, object?> values) =>
        JsonSerializer.Serialize(values);
}
