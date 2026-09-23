/** Mirrors the API's role policies (see Program.cs AddPolicy calls) so the UI can match them 1:1. */
export const VIEWER_UP = ['TenantAdmin', 'Manager', 'Staff', 'Viewer', 'SuperAdmin'];
export const STAFF_UP = ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'];
export const MANAGER_UP = ['TenantAdmin', 'Manager', 'SuperAdmin'];
export const TENANT_ADMIN_UP = ['TenantAdmin', 'SuperAdmin'];
export const SUPER_ADMIN_ONLY = ['SuperAdmin'];
