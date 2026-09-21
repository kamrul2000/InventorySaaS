import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchableSelectModule } from '../../shared/searchable-select/searchable-select.module';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Observable } from 'rxjs';
import { ReportService } from '../../core/services/report.service';
import { WarehouseService } from '../../core/services/warehouse.service';
import { CategoryService } from '../../core/services/category.service';
import { NotificationService } from '../../core/services/notification.service';
import {
  StockSummaryReportDto, LowStockReportDto, ExpiryReportDto,
  InventoryValuationDto, WarehouseDto, CategoryDto,
  AgingReportDto, SalesSummaryDto, PurchaseSummaryDto, ProfitabilityDto,
} from '../../core/models/domain.models';

/** Tab indices, so the switch statements read as something other than magic numbers. */
const Tab = {
  StockSummary: 0,
  LowStock: 1,
  Expiry: 2,
  Valuation: 3,
  ArAging: 4,
  ApAging: 5,
  Sales: 6,
  Purchases: 7,
  Profitability: 8,
} as const;

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    SearchableSelectModule,
    CommonModule, FormsModule, MatIconModule,
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.css',
})
export class ReportsComponent implements OnInit {
  readonly Tab = Tab;

  warehouses: WarehouseDto[] = [];
  categories: CategoryDto[] = [];
  warehouseFilter = '';
  categoryFilter = '';
  activeTab: number = Tab.StockSummary;
  loading = false;
  exporting = false;

  /** Date-range filter for the trading reports; blank means "everything". */
  startDate = '';
  endDate = '';

  /** Aging is a snapshot, so it takes a single as-of date rather than a range. */
  asOfDate = '';

  stockSummary: StockSummaryReportDto[] = [];
  lowStock: LowStockReportDto[] = [];
  expiry: ExpiryReportDto[] = [];
  valuation: InventoryValuationDto[] = [];
  arAging: AgingReportDto[] = [];
  apAging: AgingReportDto[] = [];
  sales: SalesSummaryDto[] = [];
  purchases: PurchaseSummaryDto[] = [];
  profitability: ProfitabilityDto[] = [];

  private tabNames = [
    'Stock_Summary', 'Low_Stock', 'Expiry', 'Inventory_Valuation',
    'AR_Aging', 'AP_Aging', 'Sales_Summary', 'Purchase_Summary', 'Profitability',
  ];

  constructor(
    private reportService: ReportService,
    private warehouseService: WarehouseService,
    private categoryService: CategoryService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.searchWarehouses('');
    this.searchCategories('');
    this.loadCurrentTab();
  }

  searchWarehouses(search: string): void {
    this.warehouseService.getAll({ pageSize: 100, search }).subscribe({ next: (r) => this.warehouses = r.items });
  }

  searchCategories(search: string): void {
    this.categoryService.getAll({ pageSize: 100, search }).subscribe({ next: (r) => this.categories = r.items });
  }

  onTabChange(index: number): void {
    this.activeTab = index;
    this.loadCurrentTab();
  }

  get showsWarehouseFilter(): boolean {
    return this.activeTab <= Tab.Expiry;
  }

  get showsDateRange(): boolean {
    return this.activeTab >= Tab.Sales;
  }

  get showsAsOf(): boolean {
    return this.activeTab === Tab.ArAging || this.activeTab === Tab.ApAging;
  }

  private get range() {
    return { startDate: this.startDate || undefined, endDate: this.endDate || undefined };
  }

  loadCurrentTab(): void {
    this.loading = true;
    const done = <T>(assign: (data: T) => void) => ({
      next: (d: T) => { assign(d); this.loading = false; },
      error: () => { this.loading = false; },
    });

    const asOf = this.asOfDate || undefined;

    switch (this.activeTab) {
      case Tab.StockSummary:
        this.reportService.stockSummary({
          warehouseId: this.warehouseFilter || undefined,
          categoryId: this.categoryFilter || undefined,
        }).subscribe(done<StockSummaryReportDto[]>(d => this.stockSummary = d));
        break;
      case Tab.LowStock:
        this.reportService.lowStock({ warehouseId: this.warehouseFilter || undefined })
          .subscribe(done<LowStockReportDto[]>(d => this.lowStock = d));
        break;
      case Tab.Expiry:
        this.reportService.expiry({ warehouseId: this.warehouseFilter || undefined })
          .subscribe(done<ExpiryReportDto[]>(d => this.expiry = d));
        break;
      case Tab.Valuation:
        // Inventory valuation aggregates across all warehouses; backend ignores warehouse filter here.
        this.reportService.inventoryValuation()
          .subscribe(done<InventoryValuationDto[]>(d => this.valuation = d));
        break;
      case Tab.ArAging:
        this.reportService.arAging(asOf).subscribe(done<AgingReportDto[]>(d => this.arAging = d));
        break;
      case Tab.ApAging:
        this.reportService.apAging(asOf).subscribe(done<AgingReportDto[]>(d => this.apAging = d));
        break;
      case Tab.Sales:
        this.reportService.salesSummary(this.range).subscribe(done<SalesSummaryDto[]>(d => this.sales = d));
        break;
      case Tab.Purchases:
        this.reportService.purchaseSummary(this.range)
          .subscribe(done<PurchaseSummaryDto[]>(d => this.purchases = d));
        break;
      case Tab.Profitability:
        this.reportService.profitability(this.range)
          .subscribe(done<ProfitabilityDto[]>(d => this.profitability = d));
        break;
    }
  }

  // ---- Column totals, so each table can show a footer without recomputing in the template ----

  sum<T>(rows: T[], field: keyof T): number {
    return rows.reduce((total, row) => total + (Number(row[field]) || 0), 0);
  }

  get agingRows(): AgingReportDto[] {
    return this.activeTab === Tab.ArAging ? this.arAging : this.apAging;
  }

  get overallMargin(): number {
    const revenue = this.sum(this.profitability, 'revenue');
    if (!revenue) return 0;
    return (this.sum(this.profitability, 'grossProfit') / revenue) * 100;
  }

  exportPdf(): void {
    this.exporting = true;
    const asOf = this.asOfDate || undefined;
    let download$: Observable<Blob>;

    switch (this.activeTab) {
      case Tab.StockSummary:
        download$ = this.reportService.downloadStockSummaryPdf({
          warehouseId: this.warehouseFilter || undefined,
          categoryId: this.categoryFilter || undefined,
        });
        break;
      case Tab.LowStock:    download$ = this.reportService.downloadLowStockPdf(); break;
      case Tab.Expiry:      download$ = this.reportService.downloadExpiryPdf(); break;
      case Tab.Valuation:   download$ = this.reportService.downloadInventoryValuationPdf(); break;
      case Tab.ArAging:     download$ = this.reportService.downloadArAgingPdf(asOf); break;
      case Tab.ApAging:     download$ = this.reportService.downloadApAgingPdf(asOf); break;
      case Tab.Sales:       download$ = this.reportService.downloadSalesSummaryPdf(this.range); break;
      case Tab.Purchases:   download$ = this.reportService.downloadPurchaseSummaryPdf(this.range); break;
      case Tab.Profitability: download$ = this.reportService.downloadProfitabilityPdf(this.range); break;
      default:
        this.exporting = false;
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
