import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SupplierDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, DataTableComponent],
  template: `
    <div class="page-header">
      <h1>Suppliers</h1>
      <button mat-flat-button color="primary" (click)="addSupplier()">
        <mat-icon>add</mat-icon> Add Supplier
      </button>
    </div>
    <app-data-table
      [columns]="columns"
      [data]="suppliers"
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
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .page-header h1 { margin: 0; }
  `],
})
export class SupplierListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name' },
    { key: 'code', label: 'Code' },
    { key: 'contactPerson', label: 'Contact' },
    { key: 'email', label: 'Email' },
    { key: 'phone', label: 'Phone' },
    { key: 'city', label: 'City' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  suppliers: SupplierDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(
    private supplierService: SupplierService,
    private router: Router,
    private dialog: MatDialog,
    private notification: NotificationService
  ) {}

  ngOnInit(): void { this.loadSuppliers(); }

  loadSuppliers(): void {
    this.loading = true;
    this.supplierService.getAll({ pageNumber: this.pageNumber, pageSize: this.pageSize, searchTerm: this.searchTerm }).subscribe({
      next: (r) => { this.suppliers = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  addSupplier(): void { this.router.navigate(['/suppliers/new']); }

  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadSuppliers(); }
  onSortChange(_s: Sort): void { this.loadSuppliers(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadSuppliers(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const s = event.row as SupplierDto;
    if (event.action === 'edit') {
      this.router.navigate(['/suppliers', s.id, 'edit']);
    } else if (event.action === 'delete') {
      this.dialog.open(ConfirmDialogComponent, { data: { title: 'Delete Supplier', message: `Delete "${s.name}"?` } })
        .afterClosed().subscribe((c) => { if (c) this.supplierService.delete(s.id).subscribe({ next: () => { this.notification.success('Deleted'); this.loadSuppliers(); } }); });
    }
  }
}
