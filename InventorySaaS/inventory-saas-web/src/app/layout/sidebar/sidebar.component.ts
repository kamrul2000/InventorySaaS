import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../core/services/auth.service';

interface NavItem {
  label: string;
  icon: string;
  route: string;
  roles?: string[];
}

@Component({
  selector: 'app-sidebar',
  standalone: true,
  imports: [CommonModule, RouterModule, MatListModule, MatIconModule],
  template: `
    <div class="sidebar-header">
      <h2>InventorySaaS</h2>
    </div>
    <mat-nav-list>
      @for (item of visibleNavItems; track item.route) {
        <a mat-list-item [routerLink]="item.route" routerLinkActive="active-link">
          <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
          <span matListItemTitle>{{ item.label }}</span>
        </a>
      }
    </mat-nav-list>
  `,
  styles: [`
    :host {
      display: flex;
      flex-direction: column;
      height: 100%;
    }
    .sidebar-header {
      padding: 16px;
      text-align: center;
      border-bottom: 1px solid rgba(0, 0, 0, 0.12);
    }
    .sidebar-header h2 {
      margin: 0;
      font-size: 1.25rem;
      font-weight: 500;
      color: var(--mat-sys-primary);
    }
    .active-link {
      background-color: var(--mat-sys-primary-container) !important;
      color: var(--mat-sys-on-primary-container) !important;
    }
  `],
})
export class SidebarComponent {
  private navItems: NavItem[] = [
    { label: 'Dashboard', icon: 'dashboard', route: '/dashboard' },
    { label: 'Products', icon: 'inventory_2', route: '/products' },
    { label: 'Categories', icon: 'category', route: '/categories' },
    { label: 'Warehouses', icon: 'warehouse', route: '/warehouses' },
    { label: 'Inventory', icon: 'assessment', route: '/inventory' },
    { label: 'Suppliers', icon: 'local_shipping', route: '/suppliers' },
    { label: 'Customers', icon: 'people', route: '/customers' },
    { label: 'Purchase Orders', icon: 'shopping_cart', route: '/purchase-orders' },
    { label: 'Sales Orders', icon: 'point_of_sale', route: '/sales-orders' },
    { label: 'Reports', icon: 'bar_chart', route: '/reports' },
    { label: 'User Management', icon: 'manage_accounts', route: '/users', roles: ['TenantAdmin', 'SuperAdmin'] },
    { label: 'Settings', icon: 'settings', route: '/settings', roles: ['TenantAdmin', 'SuperAdmin'] },
  ];

  constructor(private authService: AuthService) {}

  get visibleNavItems(): NavItem[] {
    return this.navItems.filter((item) => {
      if (!item.roles) return true;
      const userRoles = this.authService.getUserRoles();
      return item.roles.some((role) => userRoles.includes(role));
    });
  }
}
