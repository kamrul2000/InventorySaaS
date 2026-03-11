namespace InventorySaaS.Domain.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    Guid? TenantId { get; }
    bool IsSuperAdmin { get; }
    IReadOnlyList<string> Roles { get; }
}
