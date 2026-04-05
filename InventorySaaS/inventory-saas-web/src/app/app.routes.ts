import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { roleGuard } from './core/guards/role.guard';
import { MainLayoutComponent } from './layout/main-layout/main-layout.component';
import { LoginComponent } from './features/auth/login/login.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { ForgotPasswordComponent } from './features/auth/forgot-password/forgot-password.component';
import { ResetPasswordComponent } from './features/auth/reset-password/reset-password.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { ProductListComponent } from './features/products/product-list/product-list.component';
import { ProductFormComponent } from './features/products/product-form/product-form.component';
import { CategoryListComponent } from './features/categories/category-list/category-list.component';
import { WarehouseListComponent } from './features/warehouses/warehouse-list/warehouse-list.component';
import { WarehouseFormComponent } from './features/warehouses/warehouse-form/warehouse-form.component';
import { InventoryListComponent } from './features/inventory/inventory-list/inventory-list.component';
import { StockInComponent } from './features/inventory/stock-in/stock-in.component';
import { StockTransferComponent } from './features/inventory/stock-transfer/stock-transfer.component';
import { SupplierListComponent } from './features/suppliers/supplier-list/supplier-list.component';
import { SupplierFormComponent } from './features/suppliers/supplier-form/supplier-form.component';
import { CustomerListComponent } from './features/customers/customer-list/customer-list.component';
import { CustomerFormComponent } from './features/customers/customer-form/customer-form.component';
import { PoListComponent } from './features/purchase-orders/po-list/po-list.component';
import { PoFormComponent } from './features/purchase-orders/po-form/po-form.component';
import { PoDetailComponent } from './features/purchase-orders/po-detail/po-detail.component';
import { SoListComponent } from './features/sales-orders/so-list/so-list.component';
import { SoFormComponent } from './features/sales-orders/so-form/so-form.component';
import { SoDetailComponent } from './features/sales-orders/so-detail/so-detail.component';
import { ReportsComponent } from './features/reports/reports.component';
import { NotificationsComponent } from './features/notifications/notifications.component';
import { UserListComponent } from './features/users/user-list/user-list.component';
import { UserFormComponent } from './features/users/user-form/user-form.component';
import { SettingsComponent } from './features/settings/settings.component';
import { ProductDetailComponent } from './features/products/product-detail/product-detail.component';
import { CategoryDetailComponent } from './features/categories/category-detail/category-detail.component';
import { WarehouseDetailComponent } from './features/warehouses/warehouse-detail/warehouse-detail.component';
import { SupplierDetailComponent } from './features/suppliers/supplier-detail/supplier-detail.component';
import { CustomerDetailComponent } from './features/customers/customer-detail/customer-detail.component';
import { UserDetailComponent } from './features/users/user-detail/user-detail.component';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'auth/login', component: LoginComponent },
  { path: 'auth/register', component: RegisterComponent },
  { path: 'auth/forgot-password', component: ForgotPasswordComponent },
  { path: 'auth/reset-password', component: ResetPasswordComponent },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'products', component: ProductListComponent },
      { path: 'products/new', component: ProductFormComponent },
      { path: 'products/:id/edit', component: ProductFormComponent },
      { path: 'products/:id', component: ProductDetailComponent },
      { path: 'categories', component: CategoryListComponent },
      { path: 'categories/:id', component: CategoryDetailComponent },
      { path: 'warehouses', component: WarehouseListComponent },
      { path: 'warehouses/new', component: WarehouseFormComponent },
      { path: 'warehouses/:id/edit', component: WarehouseFormComponent },
      { path: 'warehouses/:id', component: WarehouseDetailComponent },
      { path: 'inventory', component: InventoryListComponent },
      { path: 'inventory/stock-in', component: StockInComponent },
      { path: 'inventory/transfer', component: StockTransferComponent },
      { path: 'suppliers', component: SupplierListComponent },
      { path: 'suppliers/new', component: SupplierFormComponent },
      { path: 'suppliers/:id/edit', component: SupplierFormComponent },
      { path: 'suppliers/:id', component: SupplierDetailComponent },
      { path: 'customers', component: CustomerListComponent },
      { path: 'customers/new', component: CustomerFormComponent },
      { path: 'customers/:id/edit', component: CustomerFormComponent },
      { path: 'customers/:id', component: CustomerDetailComponent },
      { path: 'purchase-orders', component: PoListComponent },
      { path: 'purchase-orders/new', component: PoFormComponent },
      { path: 'purchase-orders/:id', component: PoDetailComponent },
      { path: 'sales-orders', component: SoListComponent },
      { path: 'sales-orders/new', component: SoFormComponent },
      { path: 'sales-orders/:id', component: SoDetailComponent },
      { path: 'reports', component: ReportsComponent },
      // { path: 'notifications', component: NotificationsComponent },
      {
        path: 'users',
        component: UserListComponent,
        canActivate: [roleGuard],
        data: { roles: ['TenantAdmin', 'SuperAdmin'] },
      },
      { path: 'users/new', component: UserFormComponent, canActivate: [roleGuard], data: { roles: ['TenantAdmin', 'SuperAdmin'] } },
      { path: 'users/:id/edit', component: UserFormComponent, canActivate: [roleGuard], data: { roles: ['TenantAdmin', 'SuperAdmin'] } },
      { path: 'users/:id', component: UserDetailComponent, canActivate: [roleGuard], data: { roles: ['TenantAdmin', 'SuperAdmin'] } },
      { path: 'settings', component: SettingsComponent, canActivate: [roleGuard], data: { roles: ['TenantAdmin', 'SuperAdmin'] } },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
