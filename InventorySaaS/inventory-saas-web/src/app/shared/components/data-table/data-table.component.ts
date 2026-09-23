import { Component, EventEmitter, Input, Output, OnChanges, OnInit, OnDestroy, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule, MatTableDataSource } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Subject, Subscription } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

export interface TableColumn {
  key: string;
  label: string;
  type?: 'text' | 'date' | 'currency' | 'boolean';

  /**
   * Whether the API can order by this column. Defaults to false: the server only supports a
   * fixed set of sort keys per list, and a header that sorts nothing is worse than a plain one.
   */
  sortable?: boolean;

  /**
   * Sort key the API expects, when it differs from `key` (e.g. the `customerName` column sorts
   * by `customer`). Only meaningful with `sortable: true`.
   */
  sortKey?: string;
}

@Component({
  selector: 'app-data-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSlideToggleModule,
  ],
  templateUrl: './data-table.component.html',
  styleUrl: './data-table.component.css',
})
export class DataTableComponent implements OnChanges, OnInit, OnDestroy {
  @Input() columns: TableColumn[] = [];
  @Input() data: unknown[] = [];
  @Input() totalCount = 0;
  @Input() pageSize = 10;
  @Input() loading = false;
  /** Set by the parent when its fetch failed; shown instead of the (visually identical) empty state. */
  @Input() error: string | null = null;

  /** Whether the current user may edit rows (also gates the isActive-style toggle columns). Defaults to true so existing callers are unaffected. */
  @Input() canEdit = true;
  /** Whether the current user may delete rows. Defaults to true so existing callers are unaffected. */
  @Input() canDelete = true;

  @Output() pageChange = new EventEmitter<PageEvent>();
  @Output() sortChange = new EventEmitter<Sort>();
  @Output() searchChange = new EventEmitter<string>();
  @Output() rowAction = new EventEmitter<{ action: string; row: unknown }>();
  /** Emitted when the user clicks Retry on the error state. */
  @Output() retry = new EventEmitter<void>();

  // The table is fully server-driven (pageChange/sortChange/searchChange tell the parent to
  // re-fetch) - it never engages MatTableDataSource's own client-side paginator/sort, so the
  // @ViewChild refs that used to sit here for those were unused, leftover dead code (FE-10).

  dataSource = new MatTableDataSource<unknown>();
  searchValue = '';

  private readonly searchInput$ = new Subject<string>();
  private searchSubscription?: Subscription;

  get displayedColumns(): string[] {
    return [...this.columns.map((c) => c.key), 'actions'];
  }

  ngOnInit(): void {
    // Debounced so typing a search term fires one request after the user pauses, not one
    // request per keystroke; distinctUntilChanged skips a re-emit when nothing actually changed
    // (e.g. backspace-then-retype landing on the same value) (FE-01).
    this.searchSubscription = this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((value) => this.searchChange.emit(value));
  }

  ngOnDestroy(): void {
    this.searchSubscription?.unsubscribe();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data']) {
      this.dataSource.data = this.data;
    }
  }

  onPage(event: PageEvent): void {
    this.pageChange.emit(event);
  }

  onSort(sort: Sort): void {
    this.sortChange.emit(sort);
  }

  onSearch(): void {
    this.searchInput$.next(this.searchValue);
  }

  onRetry(): void {
    this.retry.emit();
  }

  onAction(action: string, row: unknown): void {
    this.rowAction.emit({ action, row });
  }
}
