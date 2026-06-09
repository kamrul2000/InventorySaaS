import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { SupplierBillDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-bill-list',
  standalone: true,
  imports: [CommonModule, MatIconModule, DataTableComponent],
  templateUrl: './supplier-bill-list.component.html',
  styleUrl: './supplier-bill-list.component.css',
})
export class SupplierBillListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'billNumber', label: 'Bill #' },
    { key: 'supplierName', label: 'Supplier' },
    { key: 'billDate', label: 'Date', type: 'date' },
    { key: 'dueDate', label: 'Due', type: 'date' },
    { key: 'status', label: 'Status' },
    { key: 'totalAmount', label: 'Total', type: 'currency' },
    { key: 'balanceDue', label: 'Balance', type: 'currency' },
  ];

  bills: SupplierBillDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(private billService: SupplierBillService, private router: Router) {}

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.billService.getAll({
      pageNumber: this.pageNumber, pageSize: this.pageSize, searchTerm: this.searchTerm,
    }).subscribe({
      next: (r) => { this.bills = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  create(): void { this.router.navigate(['/supplier-bills/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
  onSortChange(_s: Sort): void { this.load(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.load(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const bill = event.row as SupplierBillDto;
    if (event.action === 'edit') {
      this.router.navigate(['/supplier-bills', bill.id]);
    }
  }
}
