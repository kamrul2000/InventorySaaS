import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';
import {
  cameraUnsupportedReason,
  isCameraScanningSupported,
} from '../../../shared/scanner/barcode-detector.types';

interface ScanAction {
  label: string;
  description: string;
  icon: string;
  route: string;
  /** Mirrors the API policy guarding the operation; omitted means ViewerUp. */
  roles?: string[];
  available: boolean;
}

@Component({
  selector: 'app-scan-hub',
  standalone: true,
  imports: [CommonModule, RouterModule, MatIconModule],
  templateUrl: './scan-hub.component.html',
  styleUrl: './scan-hub.component.css',
})
export class ScanHubComponent {
  private readonly auth = inject(AuthService);

  readonly cameraSupported = isCameraScanningSupported();
  readonly cameraNotice = cameraUnsupportedReason();

  private readonly actions: ScanAction[] = [
    {
      label: 'Product Lookup',
      description: 'Scan a barcode to see stock by warehouse, bin, batch and serial.',
      icon: 'search',
      route: '/scan/lookup',
      available: true,
    },
    {
      label: 'Stock In',
      description: 'Receive stock into a bin with batch, expiry and serial numbers.',
      icon: 'login',
      route: '/scan/stock-in',
      roles: ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'],
      available: true,
    },
    {
      label: 'Stock Out',
      description: 'Issue stock with a reason, checked against what is available.',
      icon: 'logout',
      route: '/scan/stock-out',
      roles: ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'],
      available: true,
    },
    {
      label: 'Transfer',
      description: 'Move stock between warehouses and bins.',
      icon: 'swap_horiz',
      route: '/scan/transfer',
      roles: ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'],
      available: true,
    },
    {
      label: 'Order Picking',
      description: 'Pick a sales order, scanning each item as it is collected.',
      icon: 'checklist',
      route: '/scan/pick',
      roles: ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'],
      available: true,
    },
    {
      label: 'Stock Count',
      description: 'Count a warehouse or bin and send variances for approval.',
      icon: 'fact_check',
      route: '/scan/count',
      roles: ['TenantAdmin', 'Manager', 'Staff', 'SuperAdmin'],
      available: false,
    },
  ];

  get visibleActions(): ScanAction[] {
    const userRoles = this.auth.getUserRoles();
    return this.actions.filter((a) => !a.roles || a.roles.some((r) => userRoles.includes(r)));
  }
}
