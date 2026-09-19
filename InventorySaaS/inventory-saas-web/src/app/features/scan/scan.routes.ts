import { Routes } from '@angular/router';
import { roleGuard } from '../../core/guards/role.guard';

/** Mirrors the API's StaffUp policy on the inventory write endpoints. */
const STAFF_UP = {
  canActivate: [roleGuard],
  data: { roles: ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'] },
};

/**
 * Scanning screens are lazy-loaded. Unlike the rest of the app, which imports every route
 * eagerly, the camera code and its detector plumbing only matter to warehouse staff — keeping
 * them out of the initial bundle means office users never download them.
 */
export const SCAN_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./scan-hub/scan-hub.component').then((m) => m.ScanHubComponent),
  },
  {
    path: 'lookup',
    loadComponent: () =>
      import('./scan-lookup/scan-lookup.component').then((m) => m.ScanLookupComponent),
  },
  {
    path: 'stock-in',
    loadComponent: () =>
      import('./scan-stock-in/scan-stock-in.component').then((m) => m.ScanStockInComponent),
    ...STAFF_UP,
  },
  {
    path: 'stock-out',
    loadComponent: () =>
      import('./scan-stock-out/scan-stock-out.component').then((m) => m.ScanStockOutComponent),
    ...STAFF_UP,
  },
  {
    path: 'transfer',
    loadComponent: () =>
      import('./scan-transfer/scan-transfer.component').then((m) => m.ScanTransferComponent),
    ...STAFF_UP,
  },
  {
    path: 'pick',
    loadComponent: () => import('./scan-pick/scan-pick.component').then((m) => m.ScanPickComponent),
    ...STAFF_UP,
  },
  {
    // Same screen, entered with the order already chosen.
    path: 'pick/:orderId',
    loadComponent: () => import('./scan-pick/scan-pick.component').then((m) => m.ScanPickComponent),
    ...STAFF_UP,
  },
];
