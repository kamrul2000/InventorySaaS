using InventorySaaS.Domain.Common.Enums;
using InventorySaaS.Domain.Entities.Notification;
using InventorySaaS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace InventorySaaS.Infrastructure.Services.BackgroundJobs;

public class InventoryAlertJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InventoryAlertJob> _logger;

    public InventoryAlertJob(IServiceProvider serviceProvider, ILogger<InventoryAlertJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task CheckLowStockAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var lowStockItems = await db.InventoryBalances
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.QuantityOnHand <= ib.Product.ReorderLevel && ib.Product.IsActive)
            .ToListAsync();

        foreach (var item in lowStockItems)
        {
            var existingAlert = await db.Notifications
                .AnyAsync(n => n.TenantId == item.TenantId
                    && n.Type == NotificationType.LowStock
                    && n.ReferenceId == item.ProductId.ToString()
                    && !n.IsRead);

            if (!existingAlert)
            {
                db.Notifications.Add(new NotificationInfo
                {
                    TenantId = item.TenantId,
                    Type = NotificationType.LowStock,
                    Title = "Low Stock Alert",
                    Message = $"Product '{item.Product.Name}' in warehouse '{item.Warehouse.Name}' has {item.QuantityOnHand} units (reorder level: {item.Product.ReorderLevel})",
                    ReferenceId = item.ProductId.ToString()
                });
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Low stock check completed. Found {Count} low stock items.", lowStockItems.Count);
    }

    public async Task CheckExpiryAlertsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var expiringItems = await db.InventoryBalances
            .Include(ib => ib.Product)
            .Include(ib => ib.Warehouse)
            .Where(ib => ib.ExpiryDate != null
                && ib.ExpiryDate <= DateTime.UtcNow.AddDays(30)
                && ib.QuantityOnHand > 0)
            .ToListAsync();

        foreach (var item in expiringItems)
        {
            var existingAlert = await db.Notifications
                .AnyAsync(n => n.TenantId == item.TenantId
                    && n.Type == NotificationType.ExpiryAlert
                    && n.ReferenceId == item.Id.ToString()
                    && !n.IsRead);

            if (!existingAlert)
            {
                var daysUntilExpiry = (item.ExpiryDate!.Value - DateTime.UtcNow).Days;
                db.Notifications.Add(new NotificationInfo
                {
                    TenantId = item.TenantId,
                    Type = NotificationType.ExpiryAlert,
                    Title = "Expiry Alert",
                    Message = $"Product '{item.Product.Name}' batch '{item.BatchNumber}' expires in {daysUntilExpiry} days ({item.QuantityOnHand} units)",
                    ReferenceId = item.Id.ToString()
                });
            }
        }

        await db.SaveChangesAsync();
        _logger.LogInformation("Expiry check completed. Found {Count} expiring items.", expiringItems.Count);
    }
}
