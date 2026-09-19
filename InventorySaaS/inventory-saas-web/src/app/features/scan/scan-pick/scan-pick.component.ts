import { Component, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { PickingService } from '../../../core/services/picking.service';
import { ScanService } from '../../../core/services/scan.service';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { InventoryService } from '../../../core/services/inventory.service';
import { ScanTargetComponent } from '../../../shared/scanner/scan-target.component';
import { PickLineDto, PickSessionDto } from '../../../core/models/picking.models';
import { SalesOrderDto } from '../../../core/models/domain.models';
import { describeApiError } from '../scan-error';

@Component({
  selector: 'app-scan-pick',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule, ScanTargetComponent],
  templateUrl: './scan-pick.component.html',
  styleUrl: './scan-pick.component.css',
})
export class ScanPickComponent {
  @ViewChild(ScanTargetComponent) private scanTarget?: ScanTargetComponent;

  private readonly pickingService = inject(PickingService);
  private readonly scanService = inject(ScanService);
  private readonly salesOrderService = inject(SalesOrderService);
  private readonly notification = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Orders that can be picked, offered when no order has been chosen yet. */
  readonly pickableOrders = signal<SalesOrderDto[]>([]);
  readonly session = signal<PickSessionDto | null>(null);

  readonly busy = signal(false);
  readonly loading = signal(false);
  readonly confirming = signal(false);
  readonly error = signal<string | null>(null);

  /** Highlights the line the last scan touched, so the eye finds it immediately. */
  readonly lastLineId = signal<string | null>(null);

  deliverOnComplete = true;
  scanQuantity = 1;

  constructor() {
    const orderId = this.route.snapshot.paramMap.get('orderId');
    if (orderId) this.openOrder(orderId);
    else this.loadPickableOrders();
  }

  // --- order selection -------------------------------------------------------------------

  private loadPickableOrders(): void {
    this.loading.set(true);

    this.salesOrderService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.loading.set(false);
        // Only confirmed work is pickable; drafts have no stock reserved against them.
        this.pickableOrders.set(
          result.items.filter((o) => o.status === 'Confirmed' || o.status === 'PartiallyDelivered')
        );
      },
      error: () => this.loading.set(false),
    });
  }

  /** Loads an order's session, starting one if none is open. */
  openOrder(salesOrderId: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.pickingService.start(salesOrderId).subscribe({
      next: (session) => {
        this.loading.set(false);
        this.session.set(session);
        void this.router.navigate(['/scan/pick', salesOrderId], { replaceUrl: true });
        this.scanTarget?.refocus();
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.error.set(describeApiError(error));
      },
    });
  }

  // --- scanning --------------------------------------------------------------------------

  onScanned(code: string): void {
    const current = this.session();

    // Before an order is chosen, a scan is read as the order's own document barcode.
    if (!current) {
      this.resolveOrderFromScan(code);
      return;
    }

    this.pickItem(current.salesOrderId, code);
  }

  private resolveOrderFromScan(code: string): void {
    this.busy.set(true);

    this.scanService.resolve(code, ['SalesOrder']).subscribe({
      next: (result) => {
        this.busy.set(false);

        if (result.kind === 'SalesOrder' && result.document) {
          this.scanTarget?.reportSuccess(`Order ${result.document.number}`);
          this.openOrder(result.document.id);
        } else {
          this.scanTarget?.reportFailure('That is not a sales order. Scan the order sheet or pick one from the list.');
        }
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.scanTarget?.reportFailure(describeApiError(error));
      },
    });
  }

  private pickItem(salesOrderId: string, barcode: string): void {
    this.busy.set(true);
    this.error.set(null);

    this.pickingService
      .scan(
        salesOrderId,
        { barcode, quantity: this.scanQuantity },
        // A fresh key per scan: each physical pick is its own operation.
        InventoryService.newIdempotencyKey()
      )
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.session.set(result.session);
          this.lastLineId.set(result.line?.salesOrderItemId ?? null);
          this.scanQuantity = 1;

          if (result.session.isFullyPicked) {
            this.scanTarget?.reportSuccess('All items picked — ready to complete.');
          } else {
            this.scanTarget?.reportSuccess(result.message);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.scanTarget?.reportFailure(describeApiError(error));
        },
      });
  }

  /** Manual adjustment for items that cannot be scanned, e.g. a damaged label. */
  pickByProduct(line: PickLineDto, quantity: number): void {
    const current = this.session();
    if (!current || quantity <= 0) return;

    this.busy.set(true);

    this.pickingService
      .scan(
        current.salesOrderId,
        { productId: line.productId, quantity },
        InventoryService.newIdempotencyKey()
      )
      .subscribe({
        next: (result) => {
          this.busy.set(false);
          this.session.set(result.session);
          this.lastLineId.set(line.salesOrderItemId);
          this.scanTarget?.reportSuccess(result.message);
        },
        error: (error: HttpErrorResponse) => {
          this.busy.set(false);
          this.scanTarget?.reportFailure(describeApiError(error));
        },
      });
  }

  // --- completion ------------------------------------------------------------------------

  review(): void {
    if (!this.session()?.isFullyPicked) return;
    this.confirming.set(true);
  }

  cancelReview(): void {
    this.confirming.set(false);
    this.scanTarget?.refocus();
  }

  complete(): void {
    const current = this.session();
    if (!current || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.pickingService.complete(current.salesOrderId, this.deliverOnComplete).subscribe({
      next: () => {
        this.busy.set(false);
        this.confirming.set(false);
        this.notification.success(
          this.deliverOnComplete
            ? `Order ${current.orderNumber} picked and delivered`
            : `Order ${current.orderNumber} picked`
        );
        void this.router.navigate(['/sales-orders', current.salesOrderId]);
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

  cancelPicking(): void {
    const current = this.session();
    if (!current) return;

    this.busy.set(true);

    this.pickingService.cancel(current.salesOrderId).subscribe({
      next: () => {
        this.busy.set(false);
        this.notification.success(`Picking cancelled for ${current.orderNumber}`);
        this.session.set(null);
        void this.router.navigate(['/scan/pick'], { replaceUrl: true });
        this.loadPickableOrders();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeApiError(error));
      },
    });
  }

  // --- view helpers ----------------------------------------------------------------------

  /** Outstanding lines first: what still needs collecting belongs at the top of the list. */
  get orderedLines(): PickLineDto[] {
    const lines = this.session()?.lines ?? [];
    return [...lines].sort((a, b) => {
      if (a.remainingQuantity === 0 !== (b.remainingQuantity === 0)) {
        return a.remainingQuantity === 0 ? 1 : -1;
      }
      return a.productName.localeCompare(b.productName);
    });
  }

  progressPercent(session: PickSessionDto): number {
    return session.totalRequested === 0
      ? 0
      : Math.round((session.totalPicked / session.totalRequested) * 100);
  }
}
