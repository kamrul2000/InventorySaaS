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
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { WarehouseDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-warehouse-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './warehouse-list.component.html',
  styleUrl: './warehouse-list.component.css',
})
export class WarehouseListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name' },
    { key: 'code', label: 'Code' },
    { key: 'city', label: 'City' },
    { key: 'isDefault', label: 'Default', type: 'boolean' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
    { key: 'locationCount', label: 'Locations' },
  ];

  warehouses: WarehouseDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(
    private warehouseService: WarehouseService,
    private router: Router,
    private dialog: MatDialog,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadWarehouses();
  }

  loadWarehouses(): void {
    this.loading = true;
    this.warehouseService.getAll({
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      searchTerm: this.searchTerm,
    }).subscribe({
      next: (result) => {
        this.warehouses = result.items;
        this.totalCount = result.totalCount;
        this.loading = false;
      },
      error: () => { this.loading = false; },
    });
  }

  addWarehouse(): void {
    this.router.navigate(['/warehouses/new']);
  }

  onPageChange(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadWarehouses();
  }

  onSortChange(_sort: Sort): void {
    this.loadWarehouses();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.pageNumber = 1;
    this.loadWarehouses();
  }

  onRowAction(event: { action: string; row: unknown }): void {
    const warehouse = event.row as WarehouseDto;
    if (event.action === 'view') {
      this.router.navigate(['/warehouses', warehouse.id]);
    } else if (event.action === 'edit') {
      this.router.navigate(['/warehouses', warehouse.id, 'edit']);
    } else if (event.action === 'toggle:isActive') {
      this.warehouseService.update(warehouse.id, { isActive: !warehouse.isActive }).subscribe({
        next: () => this.loadWarehouses(),
      });
    } else if (event.action === 'toggle:isDefault') {
      this.warehouseService.update(warehouse.id, { isDefault: !warehouse.isDefault }).subscribe({
        next: () => this.loadWarehouses(),
      });
    } else if (event.action === 'delete') {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '420px',
        panelClass: 'confirm-dialog-panel',
        data: { title: 'Delete Warehouse', message: `Are you sure you want to delete "${warehouse.name}"?` },
      });
      dialogRef.afterClosed().subscribe((confirmed) => {
        if (confirmed) {
          this.warehouseService.delete(warehouse.id).subscribe({
            next: () => {
              this.notification.success('Warehouse deleted');
              this.loadWarehouses();
            },
          });
        }
      });
    }
  }
}
