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
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { PurchaseOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-po-list',
  standalone: true,
  imports: [CommonModule, FormsModule, MatButtonModule, MatIconModule, MatFormFieldModule, MatSelectModule, DataTableComponent],
  templateUrl: './po-list.component.html',
  styleUrl: './po-list.component.css',
})
export class PoListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'orderNumber', label: 'Order #' },
    { key: 'supplierName', label: 'Supplier' },
    { key: 'warehouseName', label: 'Warehouse' },
    { key: 'orderDate', label: 'Date', type: 'date' },
    { key: 'status', label: 'Status' },
    { key: 'totalAmount', label: 'Total', type: 'currency' },
  ];

  orders: PurchaseOrderDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  statusFilter = '';

  constructor(private poService: PurchaseOrderService, private router: Router) {}

  ngOnInit(): void { this.loadOrders(); }

  loadOrders(): void {
    this.loading = true;
    this.poService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize,
      searchTerm: this.searchTerm, status: this.statusFilter,
    }).subscribe({
      next: (r) => { this.orders = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/purchase-orders/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadOrders(); }
  onSortChange(_s: Sort): void { this.loadOrders(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadOrders(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const po = event.row as PurchaseOrderDto;
    if (event.action === 'edit') {
      this.router.navigate(['/purchase-orders', po.id]);
    }
  }
}
