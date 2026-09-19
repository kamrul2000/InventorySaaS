import { Routes } from '@angular/router';

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
];
