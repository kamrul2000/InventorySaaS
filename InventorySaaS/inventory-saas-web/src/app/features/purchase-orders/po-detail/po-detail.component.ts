import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PurchaseOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-po-detail',
  standalone: true,
  imports: [
    CommonModule, MatCardModule, MatTableModule, MatButtonModule,
    MatIconModule, MatChipsModule, MatProgressSpinnerModule,
  ],
  template: `
    @if (loading) {
      <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
    } @else if (order) {
      <div class="detail-container">
        <div class="detail-header">
          <div>
            <h1>PO #{{ order.orderNumber }}</h1>
            <span class="status-badge" [class]="'status-' + order.status.toLowerCase()">{{ order.status }}</span>
          </div>
          <div class="actions">
            @if (order.status === 'Draft' || order.status === 'Submitted') {
              <button mat-flat-button color="primary" (click)="approve()">
                <mat-icon>check</mat-icon> Approve
              </button>
            }
            @if (order.status === 'Approved') {
              <button mat-flat-button color="accent" (click)="receiveGoods()">
                <mat-icon>inventory</mat-icon> Receive Goods
              </button>
            }
            <button mat-button (click)="back()">
              <mat-icon>arrow_back</mat-icon> Back
            </button>
          </div>
        </div>

        <div class="info-grid">
          <mat-card>
            <mat-card-content>
              <p><strong>Supplier:</strong> {{ order.supplierName }}</p>
              <p><strong>Warehouse:</strong> {{ order.warehouseName }}</p>
              <p><strong>Order Date:</strong> {{ order.orderDate | date:'mediumDate' }}</p>
              <p><strong>Expected Delivery:</strong> {{ order.expectedDeliveryDate ? (order.expectedDeliveryDate | date:'mediumDate') : 'N/A' }}</p>
              <p><strong>Total Amount:</strong> {{ order.totalAmount | currency }}</p>
            </mat-card-content>
          </mat-card>
        </div>

        <mat-card class="items-card">
          <mat-card-header>
            <mat-card-title>Items</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <table mat-table [dataSource]="order.items" class="full-width">
              <ng-container matColumnDef="productName">
                <th mat-header-cell *matHeaderCellDef>Product</th>
                <td mat-cell *matCellDef="let item">{{ item.productName }}</td>
              </ng-container>
              <ng-container matColumnDef="productSku">
                <th mat-header-cell *matHeaderCellDef>SKU</th>
                <td mat-cell *matCellDef="let item">{{ item.productSku }}</td>
              </ng-container>
              <ng-container matColumnDef="quantity">
                <th mat-header-cell *matHeaderCellDef>Ordered</th>
                <td mat-cell *matCellDef="let item">{{ item.quantity }}</td>
              </ng-container>
              <ng-container matColumnDef="receivedQuantity">
                <th mat-header-cell *matHeaderCellDef>Received</th>
                <td mat-cell *matCellDef="let item">{{ item.receivedQuantity }}</td>
              </ng-container>
              <ng-container matColumnDef="unitPrice">
                <th mat-header-cell *matHeaderCellDef>Unit Price</th>
                <td mat-cell *matCellDef="let item">{{ item.unitPrice | currency }}</td>
              </ng-container>
              <ng-container matColumnDef="lineTotal">
                <th mat-header-cell *matHeaderCellDef>Total</th>
                <td mat-cell *matCellDef="let item">{{ item.lineTotal | currency }}</td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="itemColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: itemColumns;"></tr>
            </table>
          </mat-card-content>
        </mat-card>
      </div>
    }
  `,
  styles: [`
    .loading-center { display: flex; justify-content: center; padding: 48px; }
    .detail-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 24px; }
    .detail-header h1 { margin: 0 16px 0 0; display: inline; }
    .actions { display: flex; gap: 8px; }
    .info-grid { margin-bottom: 24px; }
    .items-card { margin-top: 16px; }
    .full-width { width: 100%; }
    .status-badge { padding: 4px 12px; border-radius: 12px; font-size: 0.875rem; font-weight: 500; }
    .status-draft { background: #e0e0e0; color: #424242; }
    .status-submitted { background: #e3f2fd; color: #1565c0; }
    .status-approved { background: #e8f5e9; color: #2e7d32; }
    .status-received { background: #f3e5f5; color: #7b1fa2; }
    .status-cancelled { background: #fce4ec; color: #c62828; }
  `],
})
export class PoDetailComponent implements OnInit {
  order: PurchaseOrderDto | null = null;
  loading = true;
  itemColumns = ['productName', 'productSku', 'quantity', 'receivedQuantity', 'unitPrice', 'lineTotal'];

  constructor(
    private poService: PurchaseOrderService, private route: ActivatedRoute,
    private router: Router, private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.poService.getById(id).subscribe({
        next: (order) => { this.order = order; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  approve(): void {
    if (!this.order) return;
    this.poService.approve(this.order.id).subscribe({
      next: () => { this.notification.success('Purchase order approved'); this.ngOnInit(); },
    });
  }

  receiveGoods(): void {
    if (!this.order) return;
    const data = {
      purchaseOrderId: this.order.id,
      items: this.order.items.map((item: any) => ({
        productId: item.productId,
        quantity: item.quantity - (item.receivedQuantity || 0),
        rejectedQuantity: 0,
        locationId: null,
        batchNumber: null,
        lotNumber: null,
        expiryDate: null,
      })),
      notes: null,
    };
    this.poService.receiveGoods(this.order.id, data).subscribe({
      next: () => { this.notification.success('Goods received'); this.ngOnInit(); },
    });
  }

  back(): void { this.router.navigate(['/purchase-orders']); }
}
