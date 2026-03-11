import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { SalesOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-so-list',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule, DataTableComponent],
  template: `
    <div class="page-header">
      <h1>Sales Orders</h1>
      <button mat-flat-button color="primary" (click)="create()">
        <mat-icon>add</mat-icon> Create SO
      </button>
    </div>
    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>Status</mat-label>
        <mat-select [(ngModel)]="statusFilter" (selectionChange)="loadOrders()">
          <mat-option value="">All</mat-option>
          <mat-option value="Draft">Draft</mat-option>
          <mat-option value="Confirmed">Confirmed</mat-option>
          <mat-option value="Delivered">Delivered</mat-option>
          <mat-option value="Cancelled">Cancelled</mat-option>
        </mat-select>
      </mat-form-field>
    </div>
    <app-data-table
      [columns]="columns"
      [data]="orders"
      [totalCount]="totalCount"
      [pageSize]="pageSize"
      [loading]="loading"
      (pageChange)="onPageChange($event)"
      (sortChange)="onSortChange($event)"
      (searchChange)="onSearch($event)"
      (rowAction)="onRowAction($event)">
    </app-data-table>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .page-header h1 { margin: 0; }
    .filters { margin-bottom: 16px; }
  `],
})
export class SoListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'orderNumber', label: 'Order #' },
    { key: 'customerName', label: 'Customer' },
    { key: 'warehouseName', label: 'Warehouse' },
    { key: 'orderDate', label: 'Date', type: 'date' },
    { key: 'status', label: 'Status' },
    { key: 'totalAmount', label: 'Total', type: 'currency' },
  ];

  orders: SalesOrderDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  statusFilter = '';

  constructor(private soService: SalesOrderService, private router: Router) {}

  ngOnInit(): void { this.loadOrders(); }

  loadOrders(): void {
    this.loading = true;
    this.soService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize,
      searchTerm: this.searchTerm, status: this.statusFilter,
    }).subscribe({
      next: (r) => { this.orders = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/sales-orders/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadOrders(); }
  onSortChange(_s: Sort): void { this.loadOrders(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadOrders(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const so = event.row as SalesOrderDto;
    if (event.action === 'edit') {
      this.router.navigate(['/sales-orders', so.id]);
    }
  }
}
