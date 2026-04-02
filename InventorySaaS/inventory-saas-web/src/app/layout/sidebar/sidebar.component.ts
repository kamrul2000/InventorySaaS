import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
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
  imports: [CommonModule, RouterModule, MatIconModule],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.css',
})
export class SidebarComponent {
  private mainItems: NavItem[] = [
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
  ];

  private accountItems: NavItem[] = [
    { label: 'User Management', icon: 'manage_accounts', route: '/users', roles: ['TenantAdmin', 'SuperAdmin'] },
    { label: 'Settings', icon: 'settings', route: '/settings', roles: ['TenantAdmin', 'SuperAdmin'] },
  ];

  constructor(private authService: AuthService) {}

  get mainNavItems(): NavItem[] {
    return this.filterByRole(this.mainItems);
  }

  get accountNavItems(): NavItem[] {
    return this.filterByRole(this.accountItems);
  }

  onLogout(): void {
    this.authService.logout();
  }

  private filterByRole(items: NavItem[]): NavItem[] {
    return items.filter((item) => {
      if (!item.roles) return true;
      const userRoles = this.authService.getUserRoles();
      return item.roles.some((role) => userRoles.includes(role));
    });
  }
}
