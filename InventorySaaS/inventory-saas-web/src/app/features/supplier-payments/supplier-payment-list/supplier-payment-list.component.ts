import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { SupplierPaymentService } from '../../../core/services/supplier-payment.service';
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP } from '../../../core/constants/roles';
import { SupplierPaymentDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-payment-list',
  standalone: true,
  imports: [CommonModule, MatIconModule, DataTableComponent],
  templateUrl: './supplier-payment-list.component.html',
  styleUrl: './supplier-payment-list.component.css',
})
export class SupplierPaymentListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'paymentNumber', label: 'Payment #', sortable: true, sortKey: 'paymentnumber' },
    { key: 'supplierName', label: 'Supplier', sortable: true, sortKey: 'supplier' },
    { key: 'paymentDate', label: 'Date', type: 'date' },
    { key: 'method', label: 'Method' },
    { key: 'amount', label: 'Amount', type: 'currency', sortable: true },
  ];

  payments: SupplierPaymentDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDescending = false;

  constructor(private paymentService: SupplierPaymentService, private router: Router, private authService: AuthService) {}

  get canWrite(): boolean { return this.authService.hasAnyRole(STAFF_UP); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.paymentService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize, search: this.searchTerm, sortBy: this.sortBy, sortDescending: this.sortDescending,
    }).subscribe({
      next: (r) => { this.payments = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/supplier-payments/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means default order.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.load();
  }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.load(); }

  onRowAction(event: { action: string; row: unknown }): void {
    if (event.action === 'view') {
      const payment = event.row as SupplierPaymentDto;
      this.router.navigate(['/supplier-payments', payment.id]);
    }
    // Supplier payments have no edit/delete endpoint — they're immutable once recorded.
  }
}
