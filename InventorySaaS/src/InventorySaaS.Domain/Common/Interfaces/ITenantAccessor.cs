namespace InventorySaaS.Domain.Common.Interfaces;

public interface ITenantAccessor
{
    Guid? TenantId { get; }
}
