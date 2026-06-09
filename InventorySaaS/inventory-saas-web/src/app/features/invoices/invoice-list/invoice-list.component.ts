import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { InvoiceService } from '../../../core/services/invoice.service';
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
    { key: 'invoiceNumber', label: 'Invoice #' },
    { key: 'customerName', label: 'Customer' },
    { key: 'invoiceDate', label: 'Date', type: 'date' },
    { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'status', label: 'Status' },
    { key: 'totalAmount', label: 'Total', type: 'currency' },
    { key: 'balanceDue', label: 'Balance', type: 'currency' },
  ];

  invoices: InvoiceDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(private invoiceService: InvoiceService, private router: Router) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.invoiceService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize, searchTerm: this.searchTerm,
    }).subscribe({
      next: (r) => { this.invoices = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/invoices/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
  onSortChange(_s: Sort): void { this.load(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.load(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const invoice = event.row as InvoiceDto;
    if (event.action === 'edit') {
      this.router.navigate(['/invoices', invoice.id]);
    }
  }
}
