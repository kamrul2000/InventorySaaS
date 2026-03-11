import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { InventoryService } from '../../../core/services/inventory.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { InventoryBalanceDto, InventoryTransactionDto, WarehouseDto } from '../../../core/models/domain.models';
import { StockInComponent } from '../stock-in/stock-in.component';
import { StockTransferComponent } from '../stock-transfer/stock-transfer.component';

@Component({
  selector: 'app-inventory-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTabsModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="page-header">
      <h1>Inventory</h1>
      <div class="actions">
        <button mat-flat-button color="primary" (click)="openStockIn()">
          <mat-icon>add_circle</mat-icon> Stock In
        </button>
        <button mat-stroked-button color="primary" (click)="openTransfer()">
          <mat-icon>swap_horiz</mat-icon> Transfer
        </button>
      </div>
    </div>

    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>Warehouse</mat-label>
        <mat-select [(ngModel)]="warehouseFilter" (selectionChange)="loadData()">
          <mat-option value="">All Warehouses</mat-option>
          @for (wh of warehouses; track wh.id) {
            <mat-option [value]="wh.id">{{ wh.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
    </div>

    <mat-tab-group (selectedTabChange)="onTabChange($event.index)">
      <mat-tab label="Balances">
        @if (loadingBalances) {
          <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
        } @else {
          <table mat-table [dataSource]="balances" class="full-width">
            <ng-container matColumnDef="productName">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let row">{{ row.productName }}</td>
            </ng-container>
            <ng-container matColumnDef="productSku">
              <th mat-header-cell *matHeaderCellDef>SKU</th>
              <td mat-cell *matCellDef="let row">{{ row.productSku }}</td>
            </ng-container>
            <ng-container matColumnDef="warehouseName">
              <th mat-header-cell *matHeaderCellDef>Warehouse</th>
              <td mat-cell *matCellDef="let row">{{ row.warehouseName }}</td>
            </ng-container>
            <ng-container matColumnDef="locationName">
              <th mat-header-cell *matHeaderCellDef>Location</th>
              <td mat-cell *matCellDef="let row">{{ row.locationName || '-' }}</td>
            </ng-container>
            <ng-container matColumnDef="quantityOnHand">
              <th mat-header-cell *matHeaderCellDef>On Hand</th>
              <td mat-cell *matCellDef="let row">{{ row.quantityOnHand }}</td>
            </ng-container>
            <ng-container matColumnDef="quantityReserved">
              <th mat-header-cell *matHeaderCellDef>Reserved</th>
              <td mat-cell *matCellDef="let row">{{ row.quantityReserved }}</td>
            </ng-container>
            <ng-container matColumnDef="quantityAvailable">
              <th mat-header-cell *matHeaderCellDef>Available</th>
              <td mat-cell *matCellDef="let row">{{ row.quantityAvailable }}</td>
            </ng-container>
            <ng-container matColumnDef="unitCost">
              <th mat-header-cell *matHeaderCellDef>Unit Cost</th>
              <td mat-cell *matCellDef="let row">{{ row.unitCost | currency }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="balanceColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: balanceColumns;"></tr>
          </table>
          <mat-paginator [length]="balanceTotalCount" [pageSize]="pageSize"
                         (page)="onBalancePage($event)" showFirstLastButtons></mat-paginator>
        }
      </mat-tab>

      <mat-tab label="Transactions">
        @if (loadingTransactions) {
          <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
        } @else {
          <table mat-table [dataSource]="transactions" class="full-width">
            <ng-container matColumnDef="transactionNumber">
              <th mat-header-cell *matHeaderCellDef>Number</th>
              <td mat-cell *matCellDef="let row">{{ row.transactionNumber }}</td>
            </ng-container>
            <ng-container matColumnDef="transactionType">
              <th mat-header-cell *matHeaderCellDef>Type</th>
              <td mat-cell *matCellDef="let row">
                <span class="status-badge" [class]="'status-' + row.transactionType.toLowerCase()">
                  {{ row.transactionType }}
                </span>
              </td>
            </ng-container>
            <ng-container matColumnDef="productName">
              <th mat-header-cell *matHeaderCellDef>Product</th>
              <td mat-cell *matCellDef="let row">{{ row.productName }}</td>
            </ng-container>
            <ng-container matColumnDef="warehouseName">
              <th mat-header-cell *matHeaderCellDef>Warehouse</th>
              <td mat-cell *matCellDef="let row">{{ row.warehouseName }}</td>
            </ng-container>
            <ng-container matColumnDef="quantity">
              <th mat-header-cell *matHeaderCellDef>Qty</th>
              <td mat-cell *matCellDef="let row">{{ row.quantity }}</td>
            </ng-container>
            <ng-container matColumnDef="transactionDate">
              <th mat-header-cell *matHeaderCellDef>Date</th>
              <td mat-cell *matCellDef="let row">{{ row.transactionDate | date:'medium' }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="txColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: txColumns;"></tr>
          </table>
          <mat-paginator [length]="txTotalCount" [pageSize]="pageSize"
                         (page)="onTxPage($event)" showFirstLastButtons></mat-paginator>
        }
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 16px; }
    .page-header h1 { margin: 0; }
    .actions { display: flex; gap: 8px; }
    .filters { margin-bottom: 16px; }
    .full-width { width: 100%; }
    .loading-center { display: flex; justify-content: center; padding: 48px; }
    .status-badge { padding: 4px 8px; border-radius: 12px; font-size: 0.75rem; font-weight: 500; }
    .status-stockin { background: #e8f5e9; color: #2e7d32; }
    .status-stockout { background: #fce4ec; color: #c62828; }
    .status-transfer { background: #e3f2fd; color: #1565c0; }
    .status-adjustment { background: #fff3e0; color: #e65100; }
  `],
})
export class InventoryListComponent implements OnInit {
  balances: InventoryBalanceDto[] = [];
  transactions: InventoryTransactionDto[] = [];
  warehouses: WarehouseDto[] = [];
  warehouseFilter = '';
  pageSize = 10;
  balancePage = 1;
  txPage = 1;
  balanceTotalCount = 0;
  txTotalCount = 0;
  loadingBalances = false;
  loadingTransactions = false;
  activeTab = 0;

  balanceColumns = ['productName', 'productSku', 'warehouseName', 'locationName', 'quantityOnHand', 'quantityReserved', 'quantityAvailable', 'unitCost'];
  txColumns = ['transactionNumber', 'transactionType', 'productName', 'warehouseName', 'quantity', 'transactionDate'];

  constructor(
    private inventoryService: InventoryService,
    private warehouseService: WarehouseService,
    private dialog: MatDialog
  ) {}

  ngOnInit(): void {
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => this.warehouses = result.items,
    });
    this.loadData();
  }

  loadData(): void {
    if (this.activeTab === 0) this.loadBalances();
    else this.loadTransactions();
  }

  loadBalances(): void {
    this.loadingBalances = true;
    this.inventoryService.getBalances({
      pageNumber: this.balancePage,
      pageSize: this.pageSize,
      warehouseId: this.warehouseFilter || undefined,
    }).subscribe({
      next: (result) => {
        this.balances = result.items;
        this.balanceTotalCount = result.totalCount;
        this.loadingBalances = false;
      },
      error: () => { this.loadingBalances = false; },
    });
  }

  loadTransactions(): void {
    this.loadingTransactions = true;
    this.inventoryService.getTransactions({
      pageNumber: this.txPage,
      pageSize: this.pageSize,
      warehouseId: this.warehouseFilter || undefined,
    }).subscribe({
      next: (result) => {
        this.transactions = result.items;
        this.txTotalCount = result.totalCount;
        this.loadingTransactions = false;
      },
      error: () => { this.loadingTransactions = false; },
    });
  }

  onTabChange(index: number): void {
    this.activeTab = index;
    this.loadData();
  }

  onBalancePage(event: PageEvent): void {
    this.balancePage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadBalances();
  }

  onTxPage(event: PageEvent): void {
    this.txPage = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadTransactions();
  }

  openStockIn(): void {
    const dialogRef = this.dialog.open(StockInComponent, { width: '600px' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) this.loadData();
    });
  }

  openTransfer(): void {
    const dialogRef = this.dialog.open(StockTransferComponent, { width: '600px' });
    dialogRef.afterClosed().subscribe((result) => {
      if (result) this.loadData();
    });
  }
}
