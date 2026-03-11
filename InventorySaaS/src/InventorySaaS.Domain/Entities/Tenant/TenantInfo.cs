using InventorySaaS.Domain.Common;
using InventorySaaS.Domain.Common.Enums;

namespace InventorySaaS.Domain.Entities.Tenant;

public class TenantInfo : BaseEntity
{
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Subdomain { get; set; }
    public string? LogoUrl { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Currency { get; set; } = "USD";
    public string? Timezone { get; set; } = "UTC";
    public TenantStatus Status { get; set; } = TenantStatus.Active;
    public Guid SubscriptionPlanId { get; set; }
    public DateTime? SubscriptionExpiresAt { get; set; }
    public string? SettingsJson { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; } = default!;
    public ICollection<Identity.ApplicationUser> Users { get; set; } = [];
}
