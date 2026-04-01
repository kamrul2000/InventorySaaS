import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SalesOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-so-detail',
  standalone: true,
  imports: [
    CommonModule, MatCardModule, MatTableModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule,
  ],
  template: `
    @if (loading) {
      <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
    } @else if (order) {
      <div class="detail-container">
        <div class="detail-header">
          <div>
            <h1>SO #{{ order.orderNumber }}</h1>
            <span class="status-badge" [class]="'status-' + order.status.toLowerCase()">{{ order.status }}</span>
          </div>
          <div class="actions">
            @if (order.status === 'Draft') {
              <button mat-flat-button color="primary" (click)="confirm()">
                <mat-icon>check</mat-icon> Confirm
              </button>
            }
            @if (order.status === 'Confirmed') {
              <button mat-flat-button color="accent" (click)="deliver()">
                <mat-icon>local_shipping</mat-icon> Deliver
              </button>
            }
            <button mat-button (click)="back()">
              <mat-icon>arrow_back</mat-icon> Back
            </button>
          </div>
        </div>

        <mat-card class="info-card">
          <mat-card-content>
            <p><strong>Customer:</strong> {{ order.customerName }}</p>
            <p><strong>Warehouse:</strong> {{ order.warehouseName }}</p>
            <p><strong>Order Date:</strong> {{ order.orderDate | date:'mediumDate' }}</p>
            <p><strong>Delivery Date:</strong> {{ order.deliveryDate ? (order.deliveryDate | date:'mediumDate') : 'N/A' }}</p>
            <p><strong>Total Amount:</strong> {{ order.totalAmount | currency }}</p>
          </mat-card-content>
        </mat-card>

        <mat-card class="items-card">
          <mat-card-header><mat-card-title>Items</mat-card-title></mat-card-header>
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
              <ng-container matColumnDef="deliveredQuantity">
                <th mat-header-cell *matHeaderCellDef>Delivered</th>
                <td mat-cell *matCellDef="let item">{{ item.deliveredQuantity }}</td>
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
    .info-card { margin-bottom: 16px; }
    .items-card { margin-top: 16px; }
    .full-width { width: 100%; }
    .status-badge { padding: 4px 12px; border-radius: 12px; font-size: 0.875rem; font-weight: 500; }
    .status-draft { background: #e0e0e0; color: #424242; }
    .status-confirmed { background: #e8f5e9; color: #2e7d32; }
    .status-delivered { background: #f3e5f5; color: #7b1fa2; }
    .status-cancelled { background: #fce4ec; color: #c62828; }
  `],
})
export class SoDetailComponent implements OnInit {
  order: SalesOrderDto | null = null;
  loading = true;
  itemColumns = ['productName', 'productSku', 'quantity', 'deliveredQuantity', 'unitPrice', 'lineTotal'];

  constructor(
    private soService: SalesOrderService, private route: ActivatedRoute,
    private router: Router, private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.soService.getById(id).subscribe({
        next: (order) => { this.order = order; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  confirm(): void {
    if (!this.order) return;
    this.soService.confirm(this.order.id).subscribe({
      next: () => { this.notification.success('Sales order confirmed'); this.ngOnInit(); },
    });
  }

  deliver(): void {
    if (!this.order) return;
    const data = {
      salesOrderId: this.order.id,
      items: this.order.items.map((item: any) => ({
        productId: item.productId,
        quantity: item.quantity - (item.deliveredQuantity || 0),
      })),
      notes: null,
    };
    this.soService.deliver(this.order.id, data).subscribe({
      next: () => { this.notification.success('Order delivered'); this.ngOnInit(); },
    });
  }

  back(): void { this.router.navigate(['/sales-orders']); }
}
