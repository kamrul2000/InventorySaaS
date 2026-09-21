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
import { UnitOfMeasureService } from '../../../core/services/unit-of-measure.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UnitOfMeasureDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-unit-list',
  standalone: true,
  imports: [CommonModule, RouterLink, MatButtonModule, MatIconModule, DataTableComponent],
  templateUrl: './unit-list.component.html',
  styleUrl: './unit-list.component.css',
})
export class UnitListComponent implements OnInit {
  columns: TableColumn[] = [
    { key: 'name', label: 'Name', sortable: true },
    { key: 'abbreviation', label: 'Abbreviation' },
    { key: 'productCount', label: 'Products' },
    { key: 'isActive', label: 'Active', type: 'boolean' },
  ];

  units: UnitOfMeasureDto[] = [];
  totalCount = 0;
  pageSize = 10;
  pageNumber = 1;
  loading = false;
  searchTerm = '';
  sortBy = '';
  sortDescending = false;

  constructor(
    private unitService: UnitOfMeasureService,
    private dialog: MatDialog,
    private notification: NotificationService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.loadUnits();
  }

  loadUnits(): void {
    this.loading = true;
    this.unitService
      .getAll({
        pageNumber: this.pageNumber,
        pageSize: this.pageSize,
        search: this.searchTerm,
        sortBy: this.sortBy,
        sortDescending: this.sortDescending,
      })
      .subscribe({
        next: (result) => {
          this.units = result.items;
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
    this.loadUnits();
  }

  onSortChange(sort: Sort): void {
    // Material reports 'asc' | 'desc' | ''; the API takes a boolean, and '' means default order.
    this.sortBy = sort.direction ? sort.active : '';
    this.sortDescending = sort.direction === 'desc';
    this.loadUnits();
  }

  onSearch(term: string): void {
    this.searchTerm = term;
    this.pageNumber = 1;
    this.loadUnits();
  }

  onRowAction(event: { action: string; row: unknown }): void {
    const unit = event.row as UnitOfMeasureDto;

    if (event.action === 'view' || event.action === 'edit') {
      this.router.navigate(['/units', unit.id, 'edit']);
      return;
    }

    if (event.action === 'toggle:isActive') {
      this.unitService.update(unit.id, { isActive: !unit.isActive }).subscribe({
        next: () => this.loadUnits(),
      });
      return;
    }

    if (event.action === 'delete') {
      const message =
        unit.productCount > 0
          ? `"${unit.name}" is used by ${unit.productCount} product(s) and cannot be deleted until they are reassigned.`
          : `Are you sure you want to delete "${unit.name}"?`;

      const dialogRef = this.dialog.open(ConfirmDialogComponent, {
        width: '420px',
        panelClass: 'confirm-dialog-panel',
        data: { title: 'Delete Unit', message },
      });

      dialogRef.afterClosed().subscribe((confirmed) => {
        if (confirmed) {
          this.unitService.delete(unit.id).subscribe({
            next: () => {
              this.notification.success('Unit deleted');
              this.loadUnits();
            },
          });
        }
      });
    }
  }
}
