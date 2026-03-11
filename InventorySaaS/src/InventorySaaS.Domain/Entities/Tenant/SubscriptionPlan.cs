using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Tenant;

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; set; } = default!;
    public SubscriptionPlanType PlanType { get; set; }
    public string? Description { get; set; }
    public decimal MonthlyPrice { get; set; }
    public decimal AnnualPrice { get; set; }
    public int MaxUsers { get; set; }
    public int MaxWarehouses { get; set; }
    public int MaxProducts { get; set; }
    public bool HasAdvancedReporting { get; set; }
    public bool HasApiAccess { get; set; }
    public bool IsActive { get; set; } = true;
    public string? FeaturesJson { get; set; }

    public ICollection<TenantInfo> Tenants { get; set; } = [];
}
