import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { SupplierPaymentService } from '../../../core/services/supplier-payment.service';
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
    { key: 'paymentNumber', label: 'Payment #' },
    { key: 'supplierName', label: 'Supplier' },
    { key: 'paymentDate', label: 'Date', type: 'date' },
    { key: 'method', label: 'Method' },
    { key: 'amount', label: 'Amount', type: 'currency' },
  ];

  payments: SupplierPaymentDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(private paymentService: SupplierPaymentService, private router: Router) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.paymentService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize, searchTerm: this.searchTerm,
    }).subscribe({
      next: (r) => { this.payments = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/supplier-payments/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
  onSortChange(_s: Sort): void { this.load(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.load(); }
  onRowAction(_event: { action: string; row: unknown }): void { /* read-only list */ }
}
