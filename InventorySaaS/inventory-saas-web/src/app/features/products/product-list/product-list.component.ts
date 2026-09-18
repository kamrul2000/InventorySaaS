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
import { ProductService } from '../../../core/services/product.service';
import { ProductImportService } from '../../../core/services/product-import.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ProductDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-product-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './product-list.component.html',
  styleUrl: './product-list.component.css',
})
export class ProductListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'sku', label: 'SKU', sortable: true },
    { key: 'categoryName', label: 'Category' },
    { key: 'brandName', label: 'Brand' },
    { key: 'costPrice', label: 'Cost Price', type: 'currency' },
    { key: 'sellingPrice', label: 'Selling Price', type: 'currency', sortable: true, sortKey: 'price' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  products: ProductDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDescending = false;
  exporting = false;

  constructor(
    private productService: ProductService,
    private importService: ProductImportService,
    private router: Router,
    private dialog: MatDialog,
    private notification: NotificationService
  ) {}

  importProducts(): void {
    this.router.navigate(['/products/import']);
  }

  exportProducts(): void {
    this.exporting = true;
    this.importService.exportProducts().subscribe({
      next: (blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `products_${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.exporting = false;
        this.notification.success('Products exported');
      },
      error: () => { this.exporting = false; },
    });
  }

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading = true;
    this.productService.getAll({
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      search: this.searchTerm,
      sortBy: this.sortBy,
      sortDescending: this.sortDescending,
    }).subscribe({
      next: (result) => {
        this.products = result.items;
        this.totalCount = result.totalCount;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  addProduct(): void {
    this.router.navigate(['/products/new']);
  }

  onPageChange(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadProducts();
  }

  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means back to default.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.loadProducts();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.pageNumber = 1;
    this.loadProducts();
  }

  onRowAction(event: { action: string; row: unknown }): void {
    const product = event.row as ProductDto;
    if (event.action === 'view') {
      this.router.navigate(['/products', product.id]);
    } else if (event.action === 'edit') {
      this.router.navigate(['/products', product.id, 'edit']);
    } else if (event.action === 'toggle:isActive') {
      this.productService.update(product.id, { isActive: !product.isActive }).subscribe({
        next: () => this.loadProducts(),
      });
    } else if (event.action === 'delete') {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '420px',
        panelClass: 'confirm-dialog-panel',
        data: { title: 'Delete Product', message: `Are you sure you want to delete "${product.name}"?` },
      });
      dialogRef.afterClosed().subscribe((confirmed) => {
        if (confirmed) {
          this.productService.delete(product.id).subscribe({
            next: () => {
              this.notification.success('Product deleted successfully');
              this.loadProducts();
            },
          });
        }
      });
    }
  }
}
