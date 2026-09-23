import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { InvoiceService } from '../../../core/services/invoice.service';
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP } from '../../../core/constants/roles';
import { InvoiceDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-invoice-list',
  standalone: true,
  imports: [CommonModule, MatIconModule, DataTableComponent],
  templateUrl: './invoice-list.component.html',
  styleUrl: './invoice-list.component.css',
})
export class InvoiceListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'invoiceNumber', label: 'Invoice #', sortable: true, sortKey: 'invoicenumber' },
    { key: 'customerName', label: 'Customer', sortable: true, sortKey: 'customer' },
    { key: 'invoiceDate', label: 'Date', type: 'date' },
    { key: 'dueDate', label: 'Due', type: 'date', sortable: true, sortKey: 'duedate' },
    { key: 'status', label: 'Status', sortable: true },
    { key: 'totalAmount', label: 'Total', type: 'currency', sortable: true, sortKey: 'amount' },
    { key: 'balanceDue', label: 'Balance', type: 'currency' },
  ];

  invoices: InvoiceDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDescending = false;

  constructor(private invoiceService: InvoiceService, private router: Router, private authService: AuthService) {}

  get canCreate(): boolean { return this.authService.hasAnyRole(STAFF_UP); }

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.invoiceService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize, search: this.searchTerm, sortBy: this.sortBy, sortDescending: this.sortDescending,
    }).subscribe({
      next: (r) => { this.invoices = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/invoices/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means default order.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.load();
  }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.load(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const invoice = event.row as InvoiceDto;
    if (event.action === 'view' || event.action === 'edit') {
      this.router.navigate(['/invoices', invoice.id]);
    }
  }
}
