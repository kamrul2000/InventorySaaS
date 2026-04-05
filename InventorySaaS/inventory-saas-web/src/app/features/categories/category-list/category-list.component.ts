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
import { CategoryFormComponent } from '../category-form/category-form.component';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CategoryDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-category-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './category-list.component.html',
  styleUrl: './category-list.component.css',
})
export class CategoryListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name' },
    { key: 'description', label: 'Description' },
    { key: 'productCount', label: 'Products' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  categories: CategoryDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';

  constructor(
    private categoryService: CategoryService,
    private dialog: MatDialog,
    private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {
    this.loading = true;
    this.categoryService.getAll({
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      searchTerm: this.searchTerm,
    }).subscribe({
      next: (result) => {
        this.categories = result.items;
        this.totalCount = result.totalCount;
        this.loading = false;
      },
      error: () => { this.loading = false; },
    });
  }

  openForm(category?: CategoryDto): void {
    const dialogRef = this.dialog.open(CategoryFormComponent, {
      width: '500px',
      data: { category, categories: this.categories },
    });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) this.loadCategories();
    });
  }

  onPageChange(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadCategories();
  }

  onSortChange(_sort: Sort): void {
    this.loadCategories();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.pageNumber = 1;
    this.loadCategories();
  }

  onRowAction(event: { action: string; row: unknown }): void {
    const category = event.row as CategoryDto;
    if (event.action === 'view') {
      this.router.navigate(['/categories', category.id]);
    } else if (event.action === 'edit') {
      this.openForm(category);
    } else if (event.action === 'toggle:isActive') {
      this.categoryService.update(category.id, { isActive: !category.isActive }).subscribe({
        next: () => this.loadCategories(),
      });
    } else if (event.action === 'delete') {
      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '420px',
        panelClass: 'confirm-dialog-panel',
        data: { title: 'Delete Category', message: `Are you sure you want to delete "${category.name}"?` },
      });
      dialogRef.afterClosed().subscribe((confirmed) => {
        if (confirmed) {
          this.categoryService.delete(category.id).subscribe({
            next: () => {
              this.notification.success('Category deleted');
              this.loadCategories();
            },
          });
        }
      });
    }
  }
}
