import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { PageEvent } from '@angular/material/paginator';
import { Sort } from '@angular/material/sort';
import {
  DataTableComponent,
  TableColumn,
} from '../../../shared/components/data-table/data-table.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { BrandService } from '../../../core/services/brand.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BrandDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-brand-list',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './brand-list.component.html',
  styleUrl: './brand-list.component.css',
})
export class BrandListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'description', label: 'Description' },
    { key: 'productCount', label: 'Products' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  brands: BrandDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDescending = false;

  constructor(
    private brandService: BrandService,
    private dialog: MatDialog,
    private notification: NotificationService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadBrands();
  }

  loadBrands(): void {
    this.loading = true;
    this.brandService
      .getAll({
        pageNumber: this.pageNumber,
        pageSize: this.pageSize,
        search: this.searchTerm,
        sortBy: this.sortBy,
        sortDescending: this.sortDescending,
      })
      .subscribe({
        next: (result) => {
          this.brands = result.items;
          this.totalCount = result.totalCount;
          this.loading = false;
        },
        error: () => {
          this.loading = false;
        },
      });
  }

  onPageChange(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadBrands();
  }

  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means default order.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.loadBrands();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.pageNumber = 1;
    this.loadBrands();
  }

  onRowAction(event: { action: string; row: unknown }): void {
    const brand = event.row as BrandDto;

    if (event.action === 'view' || event.action === 'edit') {
      this.router.navigate(['/brands', brand.id, 'edit']);
      return;
    }

    if (event.action === 'toggle:isActive') {
      this.brandService.update(brand.id, { isActive: !brand.isActive }).subscribe({
        next: () => this.loadBrands(),
      });
      return;
    }

    if (event.action === 'delete') {
      const message =
        brand.productCount > 0
          ? `"${brand.name}" is used by ${brand.productCount} product(s) and cannot be deleted until they are reassigned.`
          : `Are you sure you want to delete "${brand.name}"?`;

      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '420px',
        panelClass: 'confirm-dialog-panel',
        data: { title: 'Delete Brand', message },
      });

      dialogRef.afterClosed().subscribe((confirmed) => {
        if (confirmed) {
          this.brandService.delete(brand.id).subscribe({
            next: () => {
              this.notification.success('Brand deleted');
              this.loadBrands();
            },
          });
        }
      });
    }
  }
}
