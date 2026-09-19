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
import { ScanAvailabilityDto, ScannedProductDto } from '../../../core/models/scan.models';
import { WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';
import { describeApiError } from '../scan-error';

/** Which end of the transfer a scanned bin label should fill. */
type ScanTargetSide = 'source' | 'destination';

@Component({
  selector: 'app-scan-transfer',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule, ScanTargetComponent],
  templateUrl: './scan-transfer.component.html',
  styleUrl: './scan-transfer.component.css',
})
export class ScanTransferComponent {
  @ViewChild(ScanTargetComponent) private scanTarget?: ScanTargetComponent;

  private readonly inventoryService = inject(InventoryService);
  private readonly scanService = inject(ScanService);
  private readonly warehouseService = inject(WarehouseService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);

  readonly product = signal<ScannedProductDto | null>(null);
  readonly warehouses = signal<WarehouseDto[]>([]);
  readonly sourceLocations = signal<WarehouseLocationDto[]>([]);
  readonly destinationLocations = signal<WarehouseLocationDto[]>([]);
  readonly serials = signal<string[]>([]);
  readonly availability = signal<ScanAvailabilityDto | null>(null);

  /**
   * A scanned bin label is ambiguous on this screen, so the operator says which end they are
   * scanning. It flips to destination automatically once the source is set.
   */
  readonly side = signal<ScanTargetSide>('source');

  readonly busy = signal(false);
  readonly checkingStock = signal(false);
  readonly confirming = signal(false);
  readonly error = signal<string | null>(null);

  sourceWarehouseId = '';
  sourceLocationId = '';
  destinationWarehouseId = '';
  destinationLocationId = '';
  quantity = 1;
  batchNumber = '';
  notes = '';

  private idempotencyKey: string | null = null;

  constructor() {
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.warehouses.set(result.items);
        const preferred = result.items.find((w) => w.isDefault) ?? result.items[0];
        if (preferred) {
          this.sourceWarehouseId = preferred.id;
          this.loadLocations('source', preferred.id);
        }
      },
    });
  }

  setSide(side: ScanTargetSide): void {
    this.side.set(side);
    this.scanTarget?.refocus();
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
            this.applyLocation(result.location!.warehouseId, result.location!.id, result.location!.name);
            break;

          case 'Warehouse':
            this.applyWarehouse(result.warehouse!.id, result.warehouse!.name);
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

  private acceptSerial(serialNumber: string, productId: string): void {
    const current = this.product();

    if (current && current.id !== productId) {
      this.scanTarget?.reportFailure(`${serialNumber} belongs to a different product.`);
      return;
    }

    if (!current) {
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

  private applyLocation(warehouseId: string, locationId: string, name: string): void {
    if (this.side() === 'source') {
      this.sourceWarehouseId = warehouseId;
      this.loadLocations('source', warehouseId, locationId);
      this.scanTarget?.reportSuccess(`From ${name}`);
      // Most transfers scan source then destination, so advance automatically.
      this.side.set('destination');
      this.refreshAvailability();
    } else {
      this.destinationWarehouseId = warehouseId;
      this.loadLocations('destination', warehouseId, locationId);
      this.scanTarget?.reportSuccess(`To ${name}`);
    }

    this.resetKey();
  }

  private applyWarehouse(warehouseId: string, name: string): void {
    if (this.side() === 'source') {
      this.sourceWarehouseId = warehouseId;
      this.sourceLocationId = '';
      this.loadLocations('source', warehouseId);
      this.scanTarget?.reportSuccess(`From ${name}`);
      this.side.set('destination');
      this.refreshAvailability();
    } else {
      this.destinationWarehouseId = warehouseId;
      this.destinationLocationId = '';
      this.loadLocations('destination', warehouseId);
      this.scanTarget?.reportSuccess(`To ${name}`);
    }

    this.resetKey();
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
  }

  removeSerial(value: string): void {
    this.serials.update((list) => list.filter((s) => s !== value));
    this.quantity = this.serials().length;
    this.resetKey();
  }

  // --- availability and form -------------------------------------------------------------

  refreshAvailability(): void {
    const product = this.product();
    if (!product || !this.sourceWarehouseId) {
      this.availability.set(null);
      return;
    }

    this.checkingStock.set(true);

    this.scanService
      .getAvailability({
        productId: product.id,
        warehouseId: this.sourceWarehouseId,
        locationId: this.sourceLocationId || undefined,
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

  onSourceChange(): void {
    this.sourceLocationId = '';
    this.loadLocations('source', this.sourceWarehouseId);
    this.resetKey();
    this.refreshAvailability();
  }

  onDestinationChange(): void {
    this.destinationLocationId = '';
    this.loadLocations('destination', this.destinationWarehouseId);
    this.resetKey();
  }

  onScopeChange(): void {
    this.resetKey();
    this.refreshAvailability();
  }

  private loadLocations(side: ScanTargetSide, warehouseId: string, selectLocationId?: string): void {
    const target = side === 'source' ? this.sourceLocations : this.destinationLocations;

    if (!warehouseId) {
      target.set([]);
      return;
    }

    this.warehouseService.getLocations(warehouseId).subscribe({
      next: (locations) => {
        target.set(locations);
        if (!selectLocationId) return;

        if (side === 'source') this.sourceLocationId = selectLocationId;
        else this.destinationLocationId = selectLocationId;
      },
    });
  }

  warehouseName(id: string): string {
    return this.warehouses().find((w) => w.id === id)?.name ?? '—';
  }

  get sourceLocationName(): string {
    return this.sourceLocations().find((l) => l.id === this.sourceLocationId)?.name ?? '';
  }

  get destinationLocationName(): string {
    return this.destinationLocations().find((l) => l.id === this.destinationLocationId)?.name ?? '';
  }

  get blockingReason(): string | null {
    const product = this.product();
    if (!product) return 'Scan a product to begin.';
    if (!this.sourceWarehouseId) return 'Choose the source warehouse.';
    if (!this.destinationWarehouseId) return 'Choose the destination warehouse.';

    if (
      this.sourceWarehouseId === this.destinationWarehouseId &&
      (this.sourceLocationId || '') === (this.destinationLocationId || '')
    ) {
      return 'Source and destination must be different.';
    }

    if (this.quantity <= 0) return 'Quantity must be at least 1.';

    if (product.trackBatch && !this.batchNumber.trim()) {
      return `${product.name} is batch-tracked — enter the batch number.`;
    }

    if (product.trackSerial) {
      if (this.serials().length === 0) return `${product.name} is serial-tracked — scan each unit.`;
      if (this.serials().length !== this.quantity) return 'Quantity must match the number of serials scanned.';
    }

    if (this.exceedsAvailable) {
      return `Only ${this.availableQuantity} available at the source — cannot move ${this.quantity}.`;
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
      .transfer(
        {
          productId: product.id,
          sourceWarehouseId: this.sourceWarehouseId,
          sourceLocationId: this.sourceLocationId || null,
          destinationWarehouseId: this.destinationWarehouseId,
          destinationLocationId: this.destinationLocationId || null,
          quantity: this.quantity,
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
            `Moved ${transaction.quantity} × ${product.name} (${transaction.transactionNumber})`
          );
          this.scanTarget?.reportSuccess(`Moved ${transaction.quantity} × ${product.name}`);
          this.resetForNextItem();
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.confirming.set(false);
          const message = describeApiError(error);
          this.error.set(message);
          this.scanTarget?.reportFailure(message);
          this.refreshAvailability();
        },
      });
  }

  /** Keeps both ends of the route so a run of items can be moved one after another. */
  private resetForNextItem(): void {
    this.product.set(null);
    this.serials.set([]);
    this.availability.set(null);
    this.quantity = 1;
    this.batchNumber = '';
    this.notes = '';
    this.side.set('source');
    this.resetKey();
  }

  private resetKey(): void {
    this.idempotencyKey = null;
  }

  done(): void {
    void this.router.navigate(['/inventory']);
  }
}
