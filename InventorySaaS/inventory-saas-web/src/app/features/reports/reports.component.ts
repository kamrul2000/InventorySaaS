import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ReportService } from '../../core/services/report.service';
import { WarehouseService } from '../../core/services/warehouse.service';
import { CategoryService } from '../../core/services/category.service';
import {
  StockSummaryReportDto, LowStockReportDto, ExpiryReportDto,
  InventoryValuationDto, WarehouseDto, CategoryDto,
} from '../../core/models/domain.models';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatTabsModule, MatTableModule,
    MatFormFieldModule, MatSelectModule, MatCardModule, MatProgressSpinnerModule,
  ],
  template: `
    <h1>Reports</h1>

    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>Warehouse</mat-label>
        <mat-select [(ngModel)]="warehouseFilter" (selectionChange)="loadCurrentTab()">
          <mat-option value="">All Warehouses</mat-option>
          @for (wh of warehouses; track wh.id) {
            <mat-option [value]="wh.id">{{ wh.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>

      @if (activeTab === 0) {
        <mat-form-field appearance="outline">
          <mat-label>Category</mat-label>
          <mat-select [(ngModel)]="categoryFilter" (selectionChange)="loadCurrentTab()">
            <mat-option value="">All Categories</mat-option>
            @for (cat of categories; track cat.id) {
              <mat-option [value]="cat.id">{{ cat.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      }
    </div>

    <mat-tab-group (selectedTabChange)="onTabChange($event.index)">
      <mat-tab label="Stock Summary">
        @if (loading) {
          <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
        } @else {
          <table mat-table [dataSource]="stockSummary" class="full-width">
            <ng-container matColumnDef="productName"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let r">{{ r.productName }}</td></ng-container>
            <ng-container matColumnDef="sku"><th mat-header-cell *matHeaderCellDef>SKU</th><td mat-cell *matCellDef="let r">{{ r.sku }}</td></ng-container>
            <ng-container matColumnDef="categoryName"><th mat-header-cell *matHeaderCellDef>Category</th><td mat-cell *matCellDef="let r">{{ r.categoryName }}</td></ng-container>
            <ng-container matColumnDef="warehouseName"><th mat-header-cell *matHeaderCellDef>Warehouse</th><td mat-cell *matCellDef="let r">{{ r.warehouseName }}</td></ng-container>
            <ng-container matColumnDef="quantityOnHand"><th mat-header-cell *matHeaderCellDef>Qty</th><td mat-cell *matCellDef="let r">{{ r.quantityOnHand }}</td></ng-container>
            <ng-container matColumnDef="unitCost"><th mat-header-cell *matHeaderCellDef>Unit Cost</th><td mat-cell *matCellDef="let r">{{ r.unitCost | currency }}</td></ng-container>
            <ng-container matColumnDef="totalValue"><th mat-header-cell *matHeaderCellDef>Total Value</th><td mat-cell *matCellDef="let r">{{ r.totalValue | currency }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="stockColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: stockColumns;"></tr>
          </table>
        }
      </mat-tab>

      <mat-tab label="Low Stock">
        @if (loading) {
          <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
        } @else {
          <table mat-table [dataSource]="lowStock" class="full-width">
            <ng-container matColumnDef="productName"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let r">{{ r.productName }}</td></ng-container>
            <ng-container matColumnDef="sku"><th mat-header-cell *matHeaderCellDef>SKU</th><td mat-cell *matCellDef="let r">{{ r.sku }}</td></ng-container>
            <ng-container matColumnDef="warehouseName"><th mat-header-cell *matHeaderCellDef>Warehouse</th><td mat-cell *matCellDef="let r">{{ r.warehouseName }}</td></ng-container>
            <ng-container matColumnDef="currentStock"><th mat-header-cell *matHeaderCellDef>Current</th><td mat-cell *matCellDef="let r" class="text-warn">{{ r.currentStock }}</td></ng-container>
            <ng-container matColumnDef="reorderLevel"><th mat-header-cell *matHeaderCellDef>Reorder Level</th><td mat-cell *matCellDef="let r">{{ r.reorderLevel }}</td></ng-container>
            <ng-container matColumnDef="deficit"><th mat-header-cell *matHeaderCellDef>Deficit</th><td mat-cell *matCellDef="let r" class="text-danger">{{ r.deficit }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="lowStockColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: lowStockColumns;"></tr>
          </table>
        }
      </mat-tab>

      <mat-tab label="Expiry">
        @if (loading) {
          <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
        } @else {
          <table mat-table [dataSource]="expiry" class="full-width">
            <ng-container matColumnDef="productName"><th mat-header-cell *matHeaderCellDef>Product</th><td mat-cell *matCellDef="let r">{{ r.productName }}</td></ng-container>
            <ng-container matColumnDef="sku"><th mat-header-cell *matHeaderCellDef>SKU</th><td mat-cell *matCellDef="let r">{{ r.sku }}</td></ng-container>
            <ng-container matColumnDef="warehouseName"><th mat-header-cell *matHeaderCellDef>Warehouse</th><td mat-cell *matCellDef="let r">{{ r.warehouseName }}</td></ng-container>
            <ng-container matColumnDef="batchNumber"><th mat-header-cell *matHeaderCellDef>Batch</th><td mat-cell *matCellDef="let r">{{ r.batchNumber || '-' }}</td></ng-container>
            <ng-container matColumnDef="expiryDate"><th mat-header-cell *matHeaderCellDef>Expiry Date</th><td mat-cell *matCellDef="let r">{{ r.expiryDate | date:'mediumDate' }}</td></ng-container>
            <ng-container matColumnDef="quantity"><th mat-header-cell *matHeaderCellDef>Qty</th><td mat-cell *matCellDef="let r">{{ r.quantity }}</td></ng-container>
            <ng-container matColumnDef="daysUntilExpiry"><th mat-header-cell *matHeaderCellDef>Days Left</th>
              <td mat-cell *matCellDef="let r" [class.text-danger]="r.daysUntilExpiry <= 7" [class.text-warn]="r.daysUntilExpiry > 7 && r.daysUntilExpiry <= 30">
                {{ r.daysUntilExpiry }}
              </td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="expiryColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: expiryColumns;"></tr>
          </table>
        }
      </mat-tab>

      <mat-tab label="Inventory Valuation">
        @if (loading) {
          <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
        } @else {
          <table mat-table [dataSource]="valuation" class="full-width">
            <ng-container matColumnDef="categoryName"><th mat-header-cell *matHeaderCellDef>Category</th><td mat-cell *matCellDef="let r">{{ r.categoryName }}</td></ng-container>
            <ng-container matColumnDef="productCount"><th mat-header-cell *matHeaderCellDef>Products</th><td mat-cell *matCellDef="let r">{{ r.productCount }}</td></ng-container>
            <ng-container matColumnDef="totalCostValue"><th mat-header-cell *matHeaderCellDef>Cost Value</th><td mat-cell *matCellDef="let r">{{ r.totalCostValue | currency }}</td></ng-container>
            <ng-container matColumnDef="totalSellingValue"><th mat-header-cell *matHeaderCellDef>Selling Value</th><td mat-cell *matCellDef="let r">{{ r.totalSellingValue | currency }}</td></ng-container>
            <tr mat-header-row *matHeaderRowDef="valuationColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: valuationColumns;"></tr>
          </table>
        }
      </mat-tab>
    </mat-tab-group>
  `,
  styles: [`
    h1 { margin-bottom: 16px; }
    .filters { display: flex; gap: 16px; margin-bottom: 16px; }
    .full-width { width: 100%; }
    .loading-center { display: flex; justify-content: center; padding: 48px; }
    .text-warn { color: #e65100; font-weight: 500; }
    .text-danger { color: #c62828; font-weight: 500; }
  `],
})
export class ReportsComponent implements OnInit {
  warehouses: WarehouseDto[] = [];
  categories: CategoryDto[] = [];
  warehouseFilter = '';
  categoryFilter = '';
  activeTab = 0;
  loading = false;

  stockSummary: StockSummaryReportDto[] = [];
  lowStock: LowStockReportDto[] = [];
  expiry: ExpiryReportDto[] = [];
  valuation: InventoryValuationDto[] = [];

  stockColumns = ['productName', 'sku', 'categoryName', 'warehouseName', 'quantityOnHand', 'unitCost', 'totalValue'];
  lowStockColumns = ['productName', 'sku', 'warehouseName', 'currentStock', 'reorderLevel', 'deficit'];
  expiryColumns = ['productName', 'sku', 'warehouseName', 'batchNumber', 'expiryDate', 'quantity', 'daysUntilExpiry'];
  valuationColumns = ['categoryName', 'productCount', 'totalCostValue', 'totalSellingValue'];

  constructor(
    private reportService: ReportService,
    private warehouseService: WarehouseService,
    private categoryService: CategoryService
  ) {}

  ngOnInit(): void {
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({ next: (r) => this.warehouses = r.items });
    this.categoryService.getAll({ pageSize: 100 }).subscribe({ next: (r) => this.categories = r.items });
    this.loadCurrentTab();
  }

  onTabChange(index: number): void {
    this.activeTab = index;
    this.loadCurrentTab();
  }

  loadCurrentTab(): void {
    this.loading = true;
    const params = { warehouseId: this.warehouseFilter || undefined, categoryId: this.categoryFilter || undefined };

    switch (this.activeTab) {
      case 0:
        this.reportService.stockSummary(params as Record<string, string>).subscribe({
          next: (d) => { this.stockSummary = d; this.loading = false; },
          error: () => { this.loading = false; },
        });
        break;
      case 1:
        this.reportService.lowStock({ warehouseId: params.warehouseId } as Record<string, string>).subscribe({
          next: (d) => { this.lowStock = d; this.loading = false; },
          error: () => { this.loading = false; },
        });
        break;
      case 2:
        this.reportService.expiry({ warehouseId: params.warehouseId } as Record<string, string>).subscribe({
          next: (d) => { this.expiry = d; this.loading = false; },
          error: () => { this.loading = false; },
        });
        break;
      case 3:
        this.reportService.inventoryValuation({ warehouseId: params.warehouseId } as Record<string, string>).subscribe({
          next: (d) => { this.valuation = d; this.loading = false; },
          error: () => { this.loading = false; },
        });
        break;
    }
  }
}
