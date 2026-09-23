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
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP, MANAGER_UP } from '../../../core/constants/roles';
import { SupplierDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './supplier-list.component.html',
  styleUrl: './supplier-list.component.css',
})
export class SupplierListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name', sortable: true },
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
  sortBy = '';
  sortDescending = false;

  constructor(
    private supplierService: SupplierService,
    private router: Router,
    private dialog: MatDialog,
    private notification: NotificationService,
    private authService: AuthService
  ) {}

  get canWrite(): boolean { return this.authService.hasAnyRole(STAFF_UP); }
  get canDelete(): boolean { return this.authService.hasAnyRole(MANAGER_UP); }

  ngOnInit(): void { this.loadSuppliers(); }

  loadSuppliers(): void {
    this.loading = true;
    this.supplierService.getAll({ pageNumber: this.pageNumber, pageSize: this.pageSize, search: this.searchTerm, sortBy: this.sortBy, sortDescending: this.sortDescending }).subscribe({
      next: (r) => { this.suppliers = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  addSupplier(): void { this.router.navigate(['/suppliers/new']); }

  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadSuppliers(); }
  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means default order.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.loadSuppliers();
  }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadSuppliers(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const s = event.row as SupplierDto;
    if (event.action === 'view') {
      this.router.navigate(['/suppliers', s.id]);
    } else if (event.action === 'edit') {
      this.router.navigate(['/suppliers', s.id, 'edit']);
    } else if (event.action === 'toggle:isActive') {
      this.supplierService.update(s.id, { isActive: !s.isActive }).subscribe({
        next: () => this.loadSuppliers(),
      });
    } else if (event.action === 'delete') {
      this.dialog.open(ConfirmDialogComponent, { width: '420px', panelClass: 'confirm-dialog-panel', data: { title: 'Delete Supplier', message: `Delete "${s.name}"?` } })
        .afterClosed().subscribe((c) => { if (c) this.supplierService.delete(s.id).subscribe({ next: () => { this.notification.success('Deleted'); this.loadSuppliers(); } }); });
    }
  }
}
