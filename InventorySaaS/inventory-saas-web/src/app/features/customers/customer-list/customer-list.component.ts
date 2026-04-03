import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import { DataTableComponent, TableColumn } from '../../../shared/components/data-table/data-table.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { CustomerService } from '../../../core/services/customer.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CustomerDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [CommonModule, MatIconModule, DataTableComponent],
  templateUrl: './customer-list.component.html',
  styleUrl: './customer-list.component.css',
})
export class CustomerListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name' },
    { key: 'code', label: 'Code' },
    { key: 'customerType', label: 'Type' },
    { key: 'contactPerson', label: 'Contact' },
    { key: 'email', label: 'Email' },
    { key: 'phone', label: 'Phone' },
    { key: 'city', label: 'City' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  customers: CustomerDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(
    private customerService: CustomerService, private router: Router,
    private dialog: MatDialog, private notification: NotificationService
  ) {}

  ngOnInit(): void { this.loadCustomers(); }

  loadCustomers(): void {
    this.loading = true;
    this.customerService.getAll({ pageNumber: this.pageNumber, pageSize: this.pageSize, searchTerm: this.searchTerm }).subscribe({
      next: (r) => { this.customers = r.items; this.totalCount = r.totalCount; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  addCustomer(): void { this.router.navigate(['/customers/new']); }
  onPageChange(e: PageEvent): void { this.pageNumber = e.pageIndex + 1; this.pageSize = e.pageSize; this.loadCustomers(); }
  onSortChange(_s: Sort): void { this.loadCustomers(); }
  onSearch(t: string): void { this.searchTerm = t; this.pageNumber = 1; this.loadCustomers(); }

  onRowAction(event: { action: string; row: unknown }): void {
    const c = event.row as CustomerDto;
    if (event.action === 'edit') {
      this.router.navigate(['/customers', c.id, 'edit']);
    } else if (event.action === 'delete') {
      this.dialog.open(ConfirmDialogComponent, { data: { title: 'Delete Customer', message: `Delete "${c.name}"?` } })
        .afterClosed().subscribe((ok) => { if (ok) this.customerService.delete(c.id).subscribe({ next: () => { this.notification.success('Deleted'); this.loadCustomers(); } }); });
    }
  }
}
