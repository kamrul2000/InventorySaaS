namespace InventorySaaS.Domain.Common.Enums;

public static class AppRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string TenantAdmin = "TenantAdmin";
    public const string Manager = "Manager";
    public const string Staff = "Staff";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [SuperAdmin, TenantAdmin, Manager, Staff, Viewer];
    public static readonly string[] TenantRoles = [TenantAdmin, Manager, Staff, Viewer];
}
