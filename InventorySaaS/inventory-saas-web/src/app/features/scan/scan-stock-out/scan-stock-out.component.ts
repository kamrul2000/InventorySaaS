import { Component, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchableSelectModule } from '../../../shared/searchable-select/searchable-select.module';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { InventoryService } from '../../../core/services/inventory.service';
import { ScanService } from '../../../core/services/scan.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ScanTargetComponent } from '../../../shared/scanner/scan-target.component';
import { ScanAvailabilityDto, ScannedProductDto } from '../../../core/models/scan.models';
import { WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';
import { describeApiError } from '../scan-error';

/** Reasons offered for an issue; free text is still allowed via "Other". */
const ISSUE_REASONS = [
  'Sales issue',
  'Damaged',
  'Lost',
  'Expired',
  'Internal use',
  'Sample',
  'Return to supplier',
];

@Component({
  selector: 'app-scan-stock-out',
  standalone: true,
  imports: [SearchableSelectModule, CommonModule, FormsModule, RouterModule, MatIconModule, ScanTargetComponent],
  templateUrl: './scan-stock-out.component.html',
  styleUrl: './scan-stock-out.component.css',
})
export class ScanStockOutComponent {
  @ViewChild(ScanTargetComponent) private scanTarget?: ScanTargetComponent;

  private readonly inventoryService = inject(InventoryService);
  private readonly scanService = inject(ScanService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);

  readonly reasons = ISSUE_REASONS;

  readonly product = signal<ScannedProductDto | null>(null);
  readonly warehouses = signal<WarehouseDto[]>([]);
  readonly locations = signal<WarehouseLocationDto[]>([]);
  readonly serials = signal<string[]>([]);
  readonly availability = signal<ScanAvailabilityDto | null>(null);

  readonly busy = signal(false);
  readonly checkingStock = signal(false);
  readonly confirming = signal(false);
  readonly error = signal<string | null>(null);

  warehouseId = '';
  locationId = '';
  quantity = 1;
  reason = ISSUE_REASONS[0];
  batchNumber = '';
  notes = '';

  private idempotencyKey: string | null = null;

  constructor() {
    this.searchWarehouses('', true);
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

  // --- scanning -------------------------------------------------------------------------

  onScanned(code: string): void {
    this.busy.set(true);

    this.scanService.resolve(code).subscribe({
      next: (result) => {
        this.busy.set(false);

        switch (result.kind) {
          case 'Product':
          case 'ProductVariant':
            this.acceptProduct(result.product!, result.payload.batchNumber);
            break;

          case 'Serial':
            this.acceptSerial(result.serial!.serialNumber, result.serial!.productId);
            break;

          case 'Location':
            this.warehouseId = result.location!.warehouseId;
            this.loadLocations(result.location!.warehouseId, result.location!.id);
            this.scanTarget?.reportSuccess(`Issuing from ${result.location!.name}`);
            this.refreshAvailability();
            break;

          case 'Warehouse':
            this.warehouseId = result.warehouse!.id;
            this.locationId = '';
            this.loadLocations(result.warehouse!.id);
            this.scanTarget?.reportSuccess(`Issuing from ${result.warehouse!.name}`);
            this.refreshAvailability();
            break;

          default:
            this.scanTarget?.reportFailure(result.message ?? 'That code was not recognised.');
        }
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.scanTarget?.reportFailure(describeApiError(error));
      },
    });
  }

  private acceptProduct(product: ScannedProductDto, batchFromLabel?: string): void {
    const switching = this.product()?.id !== product.id;
    this.product.set(product);

    if (switching) {
      this.serials.set([]);
      this.quantity = 1;
      this.batchNumber = batchFromLabel ?? '';
    } else if (batchFromLabel) {
      this.batchNumber = batchFromLabel;
    }

    if (product.trackSerial) this.quantity = this.serials().length;

    this.resetKey();
    this.scanTarget?.reportSuccess(product.name);
    this.refreshAvailability();
  }

  /** Scanning a serial both identifies the product and adds that unit to the issue. */
  private acceptSerial(serialNumber: string, productId: string): void {
    const current = this.product();

    if (current && current.id !== productId) {
      this.scanTarget?.reportFailure(`${serialNumber} belongs to a different product.`);
      return;
    }

    if (!current) {
      // Load the product first so its tracking flags drive the rest of the form.
      this.scanService.findProduct(serialNumber).subscribe({
        next: (result) => {
          if (result.product) {
            this.product.set(result.product);
            this.addSerial(serialNumber);
          }
        },
      });
      return;
    }

    this.addSerial(serialNumber);
  }

  addSerial(raw: string): void {
    const value = raw.trim();
    if (!value) return;

    if (this.serials().some((s) => s.toLowerCase() === value.toLowerCase())) {
      this.scanTarget?.reportFailure(`${value} has already been scanned.`);
      return;
    }

    this.serials.update((list) => [...list, value]);
    this.quantity = this.serials().length;
    this.resetKey();
    this.scanTarget?.reportSuccess(`${value} added (${this.serials().length})`);
  }

  removeSerial(value: string): void {
    this.serials.update((list) => list.filter((s) => s !== value));
    this.quantity = this.serials().length;
    this.resetKey();
  }

  // --- availability ---------------------------------------------------------------------

  /**
   * Re-reads what is actually on hand for the chosen product, bin and batch. Shown before the
   * operator can post, so an over-issue is caught on screen rather than by a server rejection.
   */
  refreshAvailability(): void {
    const product = this.product();
    if (!product || !this.warehouseId) {
      this.availability.set(null);
      return;
    }

    this.checkingStock.set(true);

    this.scanService
      .getAvailability({
        productId: product.id,
        warehouseId: this.warehouseId,
        locationId: this.locationId || undefined,
        batchNumber: this.batchNumber.trim() || undefined,
      })
      .subscribe({
        next: (result) => {
          this.checkingStock.set(false);
          this.availability.set(result);
        },
        error: () => {
          this.checkingStock.set(false);
          this.availability.set(null);
        },
      });
  }

  get availableQuantity(): number {
    return this.availability()?.quantityAvailable ?? 0;
  }

  get exceedsAvailable(): boolean {
    return this.availability() !== null && this.quantity > this.availableQuantity;
  }

  // --- form ----------------------------------------------------------------------------

  onWarehouseChange(): void {
    this.locationId = '';
    this.loadLocations(this.warehouseId);
    this.resetKey();
    this.refreshAvailability();
  }

  onScopeChange(): void {
    this.resetKey();
    this.refreshAvailability();
  }

  private loadLocations(warehouseId: string, selectLocationId?: string): void {
    if (!warehouseId) {
      this.locations.set([]);
      return;
    }

    this.warehouseService.getLocations(warehouseId).subscribe({
      next: (locations) => {
        this.locations.set(locations);
        if (selectLocationId) this.locationId = selectLocationId;
      },
    });
  }

  get warehouseName(): string {
    return this.warehouses().find((w) => w.id === this.warehouseId)?.name ?? '—';
  }

  get locationName(): string {
    return this.locations().find((l) => l.id === this.locationId)?.name ?? '';
  }

  get blockingReason(): string | null {
    const product = this.product();
    if (!product) return 'Scan a product to begin.';
    if (!this.warehouseId) return 'Choose the source warehouse.';
    if (this.quantity <= 0) return 'Quantity must be at least 1.';
    if (!this.reason.trim()) return 'Choose a reason for the issue.';

    if (product.trackBatch && !this.batchNumber.trim()) {
      return `${product.name} is batch-tracked — enter the batch number.`;
    }

    if (product.trackSerial) {
      if (this.serials().length === 0) return `${product.name} is serial-tracked — scan each unit.`;
      if (this.serials().length !== this.quantity) return 'Quantity must match the number of serials scanned.';
    }

    // The server enforces this too; blocking here saves a round trip and a rejection.
    if (this.exceedsAvailable) {
      return `Only ${this.availableQuantity} available — cannot issue ${this.quantity}.`;
    }

    return null;
  }

  review(): void {
    this.error.set(null);
    if (this.blockingReason) return;

    this.idempotencyKey ??= InventoryService.newIdempotencyKey();
    this.confirming.set(true);
  }

  cancelReview(): void {
    this.confirming.set(false);
    this.scanTarget?.refocus();
  }

  confirm(): void {
    const product = this.product();
    if (!product || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.inventoryService
      .stockOut(
        {
          productId: product.id,
          warehouseId: this.warehouseId,
          locationId: this.locationId || null,
          quantity: this.quantity,
          reason: this.reason.trim(),
          batchNumber: this.batchNumber.trim() || null,
          notes: this.notes.trim() || null,
          serialNumbers: product.trackSerial ? this.serials() : null,
        },
        { idempotencyKey: this.idempotencyKey! }
      )
      .subscribe({
        next: (transaction) => {
          this.busy.set(false);
          this.confirming.set(false);
          this.notification.success(
            `Issued ${transaction.quantity} × ${product.name} (${transaction.transactionNumber})`
          );
          this.scanTarget?.reportSuccess(`Issued ${transaction.quantity} × ${product.name}`);
          this.resetForNextItem();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.confirming.set(false);
          const message = describeApiError(error);
          this.error.set(message);
          this.scanTarget?.reportFailure(message);
          // Stock may have moved under us; re-read so the screen tells the truth.
          this.refreshAvailability();
        },
      });
  }

  private resetForNextItem(): void {
    this.product.set(null);
    this.serials.set([]);
    this.availability.set(null);
    this.quantity = 1;
    this.batchNumber = '';
    this.notes = '';
    this.resetKey();
  }

  private resetKey(): void {
    this.idempotencyKey = null;
  }

  done(): void {
    void this.router.navigate(['/inventory']);
  }
}
