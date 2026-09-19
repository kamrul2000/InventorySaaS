import { Component, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { InventoryService } from '../../../core/services/inventory.service';
import { ScanService } from '../../../core/services/scan.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ScanTargetComponent } from '../../../shared/scanner/scan-target.component';
import { ScannedProductDto } from '../../../core/models/scan.models';
import { WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';
import { describeApiError } from '../scan-error';

@Component({
  selector: 'app-scan-stock-in',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule, ScanTargetComponent],
  templateUrl: './scan-stock-in.component.html',
  styleUrl: './scan-stock-in.component.css',
})
export class ScanStockInComponent {
  @ViewChild(ScanTargetComponent) private scanTarget?: ScanTargetComponent;

  private readonly inventoryService = inject(InventoryService);
  private readonly scanService = inject(ScanService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);

  readonly product = signal<ScannedProductDto | null>(null);
  readonly warehouses = signal<WarehouseDto[]>([]);
  readonly locations = signal<WarehouseLocationDto[]>([]);
  readonly serials = signal<string[]>([]);

  readonly busy = signal(false);
  readonly confirming = signal(false);
  readonly error = signal<string | null>(null);

  warehouseId = '';
  locationId = '';
  quantity = 1;
  unitCost = 0;
  batchNumber = '';
  lotNumber = '';
  expiryDate = '';
  notes = '';

  /**
   * Held for the whole confirm-and-post cycle so a retry after a dropped response replays the
   * same key rather than receiving the stock twice. Cleared only once the post succeeds.
   */
  private idempotencyKey: string | null = null;

  constructor() {
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.warehouses.set(result.items);
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
    // A bin or warehouse label sets the destination; anything else is treated as the product.
    this.busy.set(true);

    this.scanService.resolve(code).subscribe({
      next: (result) => {
        this.busy.set(false);

        switch (result.kind) {
          case 'Product':
          case 'ProductVariant':
            this.acceptProduct(result.product!, result.payload.batchNumber, result.payload.expiryDate, result.payload.quantity);
            break;

          case 'Location':
            this.acceptLocation(result.location!.warehouseId, result.location!.id, result.location!.name);
            break;

          case 'Warehouse':
            this.warehouseId = result.warehouse!.id;
            this.locationId = '';
            this.loadLocations(result.warehouse!.id);
            this.scanTarget?.reportSuccess(`Receiving into ${result.warehouse!.name}`);
            break;

          case 'Serial':
            this.scanTarget?.reportFailure(
              `Serial ${result.serial!.serialNumber} already exists in stock.`
            );
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

  private acceptProduct(
    product: ScannedProductDto,
    batchFromLabel?: string,
    expiryFromLabel?: string,
    quantityFromLabel?: number
  ): void {
    const switching = this.product()?.id !== product.id;
    this.product.set(product);

    if (switching) {
      this.serials.set([]);
      this.unitCost = product.costPrice;
      this.quantity = quantityFromLabel ?? 1;
      this.batchNumber = batchFromLabel ?? '';
      this.expiryDate = expiryFromLabel ? expiryFromLabel.substring(0, 10) : '';
    }

    // A serial-tracked product counts scans rather than taking a typed quantity.
    if (product.trackSerial) this.quantity = this.serials().length;

    this.resetKey();
    this.scanTarget?.reportSuccess(product.name);
  }

  private acceptLocation(warehouseId: string, locationId: string, name: string): void {
    this.warehouseId = warehouseId;
    this.loadLocations(warehouseId, locationId);
    this.scanTarget?.reportSuccess(`Receiving into ${name}`);
  }

  /** Adds a scanned or typed serial number to the list for a serial-tracked product. */
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
  }

  removeSerial(value: string): void {
    this.serials.update((list) => list.filter((s) => s !== value));
    this.quantity = this.serials().length;
    this.resetKey();
  }

  // --- form ----------------------------------------------------------------------------

  onWarehouseChange(): void {
    this.locationId = '';
    this.loadLocations(this.warehouseId);
    this.resetKey();
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

  /** The reason the form cannot be submitted yet, or null when it is ready. */
  get blockingReason(): string | null {
    const product = this.product();
    if (!product) return 'Scan a product to begin.';
    if (!this.warehouseId) return 'Choose the destination warehouse.';
    if (this.quantity <= 0) return 'Quantity must be at least 1.';
    if (this.unitCost < 0) return 'Unit cost cannot be negative.';

    if (product.trackBatch && !this.batchNumber.trim()) {
      return `${product.name} is batch-tracked — enter the batch number.`;
    }

    if (product.trackSerial) {
      if (this.serials().length === 0) return `${product.name} is serial-tracked — scan each unit.`;
      if (this.serials().length !== this.quantity) return 'Quantity must match the number of serials scanned.';
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
      .stockIn(
        {
          productId: product.id,
          warehouseId: this.warehouseId,
          locationId: this.locationId || null,
          quantity: this.quantity,
          unitCost: this.unitCost,
          batchNumber: this.batchNumber.trim() || null,
          lotNumber: this.lotNumber.trim() || null,
          expiryDate: this.expiryDate ? new Date(this.expiryDate).toISOString() : null,
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
            `Received ${transaction.quantity} × ${product.name} (${transaction.transactionNumber})`
          );
          this.scanTarget?.reportSuccess(`Received ${transaction.quantity} × ${product.name}`);
          this.resetForNextItem();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.confirming.set(false);
          const message = describeApiError(error);
          this.error.set(message);
          this.scanTarget?.reportFailure(message);
        },
      });
  }

  /** Clears the item but keeps the destination, which is what receiving a pallet needs. */
  private resetForNextItem(): void {
    this.product.set(null);
    this.serials.set([]);
    this.quantity = 1;
    this.unitCost = 0;
    this.batchNumber = '';
    this.lotNumber = '';
    this.expiryDate = '';
    this.notes = '';
    this.resetKey();
  }

  /** Any change to what would be posted invalidates the pending key. */
  private resetKey(): void {
    this.idempotencyKey = null;
  }

  done(): void {
    void this.router.navigate(['/inventory']);
  }
}
