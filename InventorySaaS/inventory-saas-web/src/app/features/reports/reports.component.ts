import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { ReportService } from '../../core/services/report.service';
import { WarehouseService } from '../../core/services/warehouse.service';
import { CategoryService } from '../../core/services/category.service';
import { NotificationService } from '../../core/services/notification.service';
import {
  StockSummaryReportDto, LowStockReportDto, ExpiryReportDto,
  InventoryValuationDto, WarehouseDto, CategoryDto,
} from '../../core/models/domain.models';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatIconModule,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.css',
})
export class ReportsComponent implements OnInit {
  warehouses: WarehouseDto[] = [];
  categories: CategoryDto[] = [];
  warehouseFilter = '';
  categoryFilter = '';
  activeTab = 0;
  loading = false;
  exporting = false;

  stockSummary: StockSummaryReportDto[] = [];
  lowStock: LowStockReportDto[] = [];
  expiry: ExpiryReportDto[] = [];
  valuation: InventoryValuationDto[] = [];

  stockColumns = ['productName', 'sku', 'categoryName', 'warehouseName', 'quantityOnHand', 'unitCost', 'totalValue'];
  lowStockColumns = ['productName', 'sku', 'warehouseName', 'currentStock', 'reorderLevel', 'deficit'];
  expiryColumns = ['productName', 'sku', 'warehouseName', 'batchNumber', 'expiryDate', 'quantity', 'daysUntilExpiry'];
  valuationColumns = ['categoryName', 'productCount', 'totalCostValue', 'totalSellingValue'];

  private tabNames = ['Stock_Summary', 'Low_Stock', 'Expiry', 'Inventory_Valuation'];

  constructor(
    private reportService: ReportService,
    private warehouseService: WarehouseService,
    private categoryService: CategoryService,
    private notification: NotificationService
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

  exportPdf(): void {
    this.exporting = true;
    let download$;

    switch (this.activeTab) {
      case 0:
        download$ = this.reportService.downloadStockSummaryPdf({
          warehouseId: this.warehouseFilter || undefined,
          categoryId: this.categoryFilter || undefined,
        });
        break;
      case 1:
        download$ = this.reportService.downloadLowStockPdf();
        break;
      case 2:
        download$ = this.reportService.downloadExpiryPdf();
        break;
      case 3:
        download$ = this.reportService.downloadInventoryValuationPdf();
        break;
      default:
        return;
    }

    download$.subscribe({
      next: (blob: Blob) => {
        const url = window.URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `${this.tabNames[this.activeTab]}_${new Date().toISOString().slice(0, 10)}.pdf`;
        a.click();
        window.URL.revokeObjectURL(url);
        this.exporting = false;
        this.notification.success('PDF exported successfully');
      },
      error: () => {
        this.exporting = false;
        this.notification.error('Failed to export PDF');
      },
    });
  }
}
