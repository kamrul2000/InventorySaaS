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
    { key: 'name', label: 'Name' },
    { key: 'sku', label: 'SKU' },
    { key: 'categoryName', label: 'Category' },
    { key: 'brandName', label: 'Brand' },
    { key: 'costPrice', label: 'Cost Price', type: 'currency' },
    { key: 'sellingPrice', label: 'Selling Price', type: 'currency' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  products: ProductDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDirection = '';

  constructor(
    private productService: ProductService,
    private router: Router,
    private dialog: MatDialog,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadProducts();
  }

  loadProducts(): void {
    this.loading = true;
    this.productService.getAll({
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      searchTerm: this.searchTerm,
      sortBy: this.sortBy,
      sortDirection: this.sortDirection,
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
    this.sortBy = sort.active;
    this.sortDirection = sort.direction;
    this.loadProducts();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.pageNumber = 1;
    this.loadProducts();
  }

  onRowAction(event: { action: string; row: unknown }): void {
    const product = event.row as ProductDto;
    if (event.action === 'edit') {
      this.router.navigate(['/products', product.id, 'edit']);
    } else if (event.action === 'delete') {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
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
