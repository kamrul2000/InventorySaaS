import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardDto } from '../../core/models/domain.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="dashboard">
      <h1>Dashboard</h1>

      @if (loading) {
        <div class="loading-center">
          <mat-spinner diameter="40"></mat-spinner>
        </div>
      } @else if (data) {
        <div class="kpi-grid">
          <mat-card class="kpi-card">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>inventory_2</mat-icon></div>
              <div class="kpi-info">
                <span class="kpi-value">{{ data.totalProducts }}</span>
                <span class="kpi-label">Total Products</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>warehouse</mat-icon></div>
              <div class="kpi-info">
                <span class="kpi-value">{{ data.totalWarehouses }}</span>
                <span class="kpi-label">Warehouses</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card warning">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>warning</mat-icon></div>
              <div class="kpi-info">
                <span class="kpi-value">{{ data.lowStockCount }}</span>
                <span class="kpi-label">Low Stock Items</span>
              </div>
            </mat-card-content>
          </mat-card>

          <mat-card class="kpi-card">
            <mat-card-content>
              <div class="kpi-icon"><mat-icon>attach_money</mat-icon></div>
              <div class="kpi-info">
                <span class="kpi-value">{{ data.totalInventoryValue | currency }}</span>
                <span class="kpi-label">Inventory Value</span>
              </div>
            </mat-card-content>
          </mat-card>
        </div>

        <div class="tables-grid">
          <mat-card>
            <mat-card-header>
              <mat-card-title>Recent Transactions</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <table mat-table [dataSource]="data.recentTransactions" class="full-width">
                <ng-container matColumnDef="transactionNumber">
                  <th mat-header-cell *matHeaderCellDef>Number</th>
                  <td mat-cell *matCellDef="let row">{{ row.transactionNumber }}</td>
                </ng-container>
                <ng-container matColumnDef="type">
                  <th mat-header-cell *matHeaderCellDef>Type</th>
                  <td mat-cell *matCellDef="let row">
                    <span class="status-badge" [class]="'status-' + row.type.toLowerCase()">{{ row.type }}</span>
                  </td>
                </ng-container>
                <ng-container matColumnDef="productName">
                  <th mat-header-cell *matHeaderCellDef>Product</th>
                  <td mat-cell *matCellDef="let row">{{ row.productName }}</td>
                </ng-container>
                <ng-container matColumnDef="quantity">
                  <th mat-header-cell *matHeaderCellDef>Qty</th>
                  <td mat-cell *matCellDef="let row">{{ row.quantity }}</td>
                </ng-container>
                <ng-container matColumnDef="date">
                  <th mat-header-cell *matHeaderCellDef>Date</th>
                  <td mat-cell *matCellDef="let row">{{ row.date | date:'mediumDate' }}</td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="txColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: txColumns;"></tr>
              </table>
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-header>
              <mat-card-title>Stock Alerts</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <table mat-table [dataSource]="data.stockAlerts" class="full-width">
                <ng-container matColumnDef="productName">
                  <th mat-header-cell *matHeaderCellDef>Product</th>
                  <td mat-cell *matCellDef="let row">{{ row.productName }}</td>
                </ng-container>
                <ng-container matColumnDef="sku">
                  <th mat-header-cell *matHeaderCellDef>SKU</th>
                  <td mat-cell *matCellDef="let row">{{ row.sku }}</td>
                </ng-container>
                <ng-container matColumnDef="warehouseName">
                  <th mat-header-cell *matHeaderCellDef>Warehouse</th>
                  <td mat-cell *matCellDef="let row">{{ row.warehouseName }}</td>
                </ng-container>
                <ng-container matColumnDef="currentStock">
                  <th mat-header-cell *matHeaderCellDef>Current</th>
                  <td mat-cell *matCellDef="let row" class="text-warn">{{ row.currentStock }}</td>
                </ng-container>
                <ng-container matColumnDef="reorderLevel">
                  <th mat-header-cell *matHeaderCellDef>Reorder Level</th>
                  <td mat-cell *matCellDef="let row">{{ row.reorderLevel }}</td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="alertColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: alertColumns;"></tr>
              </table>
            </mat-card-content>
          </mat-card>
        </div>
      }
    </div>
  `,
  styles: [`
    .dashboard h1 {
      margin-bottom: 24px;
    }
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    .kpi-card mat-card-content {
      display: flex;
      align-items: center;
      gap: 16px;
      padding: 16px;
    }
    .kpi-icon {
      background: var(--mat-sys-primary-container);
      border-radius: 12px;
      padding: 12px;
      display: flex;
    }
    .kpi-icon mat-icon {
      color: var(--mat-sys-on-primary-container);
      font-size: 32px;
      width: 32px;
      height: 32px;
    }
    .kpi-card.warning .kpi-icon {
      background: #fff3e0;
    }
    .kpi-card.warning .kpi-icon mat-icon {
      color: #e65100;
    }
    .kpi-info {
      display: flex;
      flex-direction: column;
    }
    .kpi-value {
      font-size: 1.5rem;
      font-weight: 600;
    }
    .kpi-label {
      font-size: 0.875rem;
      color: rgba(0, 0, 0, 0.54);
    }
    .tables-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(400px, 1fr));
      gap: 16px;
    }
    .full-width {
      width: 100%;
    }
    .loading-center {
      display: flex;
      justify-content: center;
      padding: 48px;
    }
    .status-badge {
      padding: 4px 8px;
      border-radius: 12px;
      font-size: 0.75rem;
      font-weight: 500;
    }
    .status-stockin { background: #e8f5e9; color: #2e7d32; }
    .status-stockout { background: #fce4ec; color: #c62828; }
    .status-transfer { background: #e3f2fd; color: #1565c0; }
    .status-adjustment { background: #fff3e0; color: #e65100; }
    .text-warn { color: #e65100; font-weight: 500; }
  `],
})
export class DashboardComponent implements OnInit {
  data: DashboardDto | null = null;
  loading = true;

  txColumns = ['transactionNumber', 'type', 'productName', 'quantity', 'date'];
  alertColumns = ['productName', 'sku', 'warehouseName', 'currentStock', 'reorderLevel'];

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.get().subscribe({
      next: (data) => {
        this.data = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }
}
