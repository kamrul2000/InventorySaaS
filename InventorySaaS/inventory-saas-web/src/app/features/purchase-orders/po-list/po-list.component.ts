import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchableSelectModule } from '../../../shared/searchable-select/searchable-select.module';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP } from '../../../core/constants/roles';
import { PurchaseOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-po-list',
  standalone: true,
  imports: [SearchableSelectModule, CommonModule, FormsModule, MatIconModule, DataTableComponent],
  templateUrl: './po-list.component.html',
  styleUrl: './po-list.component.css',
})
export class PoListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'orderNumber', label: 'Order #', sortable: true, sortKey: 'ordernumber' },
    { key: 'supplierName', label: 'Supplier', sortable: true, sortKey: 'supplier' },
    { key: 'warehouseName', label: 'Warehouse' },
    { key: 'orderDate', label: 'Date', type: 'date' },
    { key: 'status', label: 'Status', sortable: true },
    { key: 'totalAmount', label: 'Total', type: 'currency', sortable: true, sortKey: 'amount' },
  ];

  orders: PurchaseOrderDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDescending = false;
  statusFilter = '';

  constructor(private poService: PurchaseOrderService, private router: Router, private authService: AuthService) {}

  get canCreate(): boolean { return this.authService.hasAnyRole(STAFF_UP); }

  ngOnInit(): void { this.loadOrders(); }

  loadOrders(): void {
    this.loading = true;
    this.poService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize,
      search: this.searchTerm, sortBy: this.sortBy, sortDescending: this.sortDescending, status: this.statusFilter,
    }).subscribe({
      next: (r) => { this.orders = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/purchase-orders/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadOrders(); }
  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means default order.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.loadOrders();
  }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadOrders(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const po = event.row as PurchaseOrderDto;
    if (event.action === 'view' || event.action === 'edit') {
      this.router.navigate(['/purchase-orders', po.id]);
    }
  }
}
