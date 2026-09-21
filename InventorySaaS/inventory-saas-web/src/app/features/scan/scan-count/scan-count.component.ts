import { Component, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchableSelectModule } from '../../../shared/searchable-select/searchable-select.module';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { StockCountService } from '../../../core/services/stock-count.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { InventoryService } from '../../../core/services/inventory.service';
import { ScanTargetComponent } from '../../../shared/scanner/scan-target.component';
import { StockCountLineDto, StockCountSessionDto } from '../../../core/models/stock-count.models';
import { WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';
import { describeApiError } from '../scan-error';

@Component({
  selector: 'app-scan-count',
  standalone: true,
  imports: [SearchableSelectModule, CommonModule, FormsModule, RouterModule, MatIconModule, ScanTargetComponent],
  templateUrl: './scan-count.component.html',
  styleUrl: './scan-count.component.css',
})
export class ScanCountComponent {
  @ViewChild(ScanTargetComponent) private scanTarget?: ScanTargetComponent;

  private readonly stockCountService = inject(StockCountService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly session = signal<StockCountSessionDto | null>(null);
  readonly openCounts = signal<StockCountSessionDto[]>([]);
  readonly warehouses = signal<WarehouseDto[]>([]);
  readonly locations = signal<WarehouseLocationDto[]>([]);

  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly confirming = signal(false);
  readonly error = signal<string | null>(null);
  readonly lastLineId = signal<string | null>(null);

  warehouseId = '';
  locationId = '';
  startNotes = '';
  submitNotes = '';
  scanQuantity = 1;
  scanBatch = '';

  constructor() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.load(id);
    else this.loadStartOptions();
  }

  private loadStartOptions(): void {
    this.loading.set(true);

    this.searchWarehouses('', true);

    // Counts already under way are resumed rather than duplicated.
    this.stockCountService.getAll({ pageSize: 50, status: 'Counting' }).subscribe({
      next: (result) => {
        this.loading.set(false);
        this.openCounts.set(result.items);
      },
      error: () => this.loading.set(false),
    });
  }

  searchWarehouses(search: string, initializeSelection = false): void {
    this.warehouseService.getAll({ pageSize: 100, search }).subscribe({
      next: (result) => {
        this.warehouses.set(result.items);
        if (!initializeSelection) return;
        const preferred = result.items.find((w) => w.isDefault) ?? result.items[0];
        if (preferred) {
          this.warehouseId = preferred.id;
          this.loadLocations(preferred.id);
        }
      },
    });
  }

  private load(id: string): void {
    this.loading.set(true);

    this.stockCountService.getById(id).subscribe({
      next: (session) => {
        this.loading.set(false);
        this.session.set(session);
        this.scanTarget?.refocus();
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(describeApiError(error));
      },
    });
  }

  onWarehouseChange(): void {
    this.locationId = '';
    this.loadLocations(this.warehouseId);
  }

  private loadLocations(warehouseId: string): void {
    if (!warehouseId) {
      this.locations.set([]);
      return;
    }

    this.warehouseService.getLocations(warehouseId).subscribe({
      next: (locations) => this.locations.set(locations),
    });
  }

  start(): void {
    if (!this.warehouseId || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.stockCountService
      .start({
        warehouseId: this.warehouseId,
        locationId: this.locationId || null,
        notes: this.startNotes.trim() || null,
      })
      .subscribe({
        next: (session) => {
          this.busy.set(false);
          this.session.set(session);
          void this.router.navigate(['/scan/count', session.id], { replaceUrl: true });
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.error.set(describeApiError(error));
        },
      });
  }

  resume(session: StockCountSessionDto): void {
    void this.router.navigate(['/scan/count', session.id]);
    this.load(session.id);
  }

  // --- counting --------------------------------------------------------------------------

  onScanned(code: string): void {
    const current = this.session();
    if (!current || current.status !== 'Counting') return;

    this.busy.set(true);

    this.stockCountService
      .scan(
        current.id,
        {
          barcode: code,
          quantity: this.scanQuantity,
          batchNumber: this.scanBatch.trim() || null,
        },
        InventoryService.newIdempotencyKey()
      )
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.session.set(result.session);
          this.lastLineId.set(result.line?.id ?? null);
          this.scanQuantity = 1;

          // A variance is worth a distinct tone: it is the thing a counter must notice.
          if (result.line && result.line.variance !== 0) this.scanTarget?.reportWarning(result.message);
          else this.scanTarget?.reportSuccess(result.message);
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.scanTarget?.reportFailure(describeApiError(error));
        },
      });
  }

  /** Types an exact figure over a line, for a shelf counted by eye rather than scanned. */
  setExact(line: StockCountLineDto, quantity: number): void {
    const current = this.session();
    if (!current || quantity < 0) return;

    this.busy.set(true);

    this.stockCountService
      .scan(
        current.id,
        {
          productId: line.productId,
          quantity,
          locationId: line.locationId ?? null,
          batchNumber: line.batchNumber ?? null,
          setExactQuantity: true,
        },
        InventoryService.newIdempotencyKey()
      )
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.session.set(result.session);
          this.lastLineId.set(line.id);
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.scanTarget?.reportFailure(describeApiError(error));
        },
      });
  }

  // --- submission ------------------------------------------------------------------------

  review(): void {
    const current = this.session();
    if (!current || current.lineCount === 0) return;
    this.confirming.set(true);
  }

  cancelReview(): void {
    this.confirming.set(false);
    this.scanTarget?.refocus();
  }

  submit(): void {
    const current = this.session();
    if (!current || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.stockCountService.submit(current.id, this.submitNotes.trim() || undefined).subscribe({
      next: (session) => {
        this.busy.set(false);
        this.confirming.set(false);
        this.session.set(session);
        this.notification.success(`${session.countNumber} sent for approval`);
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.confirming.set(false);
        const message = describeApiError(error);
        this.error.set(message);
      },
    });
  }

  cancelCount(): void {
    const current = this.session();
    if (!current) return;

    this.busy.set(true);

    this.stockCountService.cancel(current.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.notification.success(`${current.countNumber} cancelled`);
        void this.router.navigate(['/scan/count'], { replaceUrl: true });
        this.session.set(null);
        this.loadStartOptions();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeApiError(error));
      },
    });
  }

  // --- view helpers ----------------------------------------------------------------------

  /** Variances first: a line that disagrees with the system is what needs a second look. */
  get orderedLines(): StockCountLineDto[] {
    const lines = this.session()?.lines ?? [];
    return [...lines].sort((a, b) => {
      const aVaries = a.variance !== 0;
      const bVaries = b.variance !== 0;
      if (aVaries !== bVaries) return aVaries ? -1 : 1;
      return a.productName.localeCompare(b.productName);
    });
  }
}
