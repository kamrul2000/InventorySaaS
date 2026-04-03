import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { SalesOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-so-list',
  standalone: true,
  imports: [CommonModule, MatIconModule, DataTableComponent],
  templateUrl: './so-list.component.html',
  styleUrl: './so-list.component.css',
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
