import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiService } from './api.service';
import {
  AgingReportDto,
  ExpiryReportDto,
  InventoryValuationDto,
  LowStockReportDto,
  ProfitabilityDto,
  PurchaseSummaryDto,
  SalesSummaryDto,
  StockSummaryReportDto,
} from '../models/domain.models';

/** Optional start/end filter shared by the trading-activity reports. */
export interface DateRange {
  startDate?: string;
  endDate?: string;
}

// Report endpoints are paginated server-side; fetch a large page so the UI gets every row.
const REPORT_PAGE_SIZE = 10000;

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly endpoint = '/api/v1/reports';

  constructor(private api: ApiService) {}

  stockSummary(params?: {
    warehouseId?: string;
    categoryId?: string;
  }): Observable<StockSummaryReportDto[]> {
    const query: Record<string, string | number | boolean> = { pageSize: REPORT_PAGE_SIZE };
    if (params?.warehouseId) query['warehouseId'] = params.warehouseId;
    if (params?.categoryId) query['categoryId'] = params.categoryId;
    return this.api.get<{ items: StockSummaryReportDto[] }>(`${this.endpoint}/stock-summary`, query)
      .pipe(map(r => r.items ?? []));
  }

  lowStock(params?: {
    warehouseId?: string;
  }): Observable<LowStockReportDto[]> {
    const query: Record<string, string | number | boolean> = { pageSize: REPORT_PAGE_SIZE };
    if (params?.warehouseId) query['warehouseId'] = params.warehouseId;
    return this.api.get<{ items: LowStockReportDto[] }>(`${this.endpoint}/low-stock`, query)
      .pipe(map(r => r.items ?? []));
  }

  expiry(params?: {
    warehouseId?: string;
    daysAhead?: number;
  }): Observable<ExpiryReportDto[]> {
    const query: Record<string, string | number | boolean> = { pageSize: REPORT_PAGE_SIZE };
    if (params?.warehouseId) query['warehouseId'] = params.warehouseId;
    if (params?.daysAhead != null) query['daysAhead'] = params.daysAhead;
    return this.api.get<{ items: ExpiryReportDto[] }>(`${this.endpoint}/expiry`, query)
      .pipe(map(r => r.items ?? []));
  }

  inventoryValuation(): Observable<InventoryValuationDto[]> {
    return this.api.get<InventoryValuationDto[]>(`${this.endpoint}/inventory-valuation`);
  }

  downloadStockSummaryPdf(params?: { warehouseId?: string; categoryId?: string }): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/stock-summary/pdf`, params as Record<string, string | number | boolean>);
  }

  downloadLowStockPdf(): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/low-stock/pdf`);
  }

  downloadExpiryPdf(params?: { daysAhead?: number }): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/expiry/pdf`, params as Record<string, string | number | boolean>);
  }

  downloadInventoryValuationPdf(): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/inventory-valuation/pdf`);
  }

  // ---- Receivables & payables ----

  arAging(asOf?: string): Observable<AgingReportDto[]> {
    return this.api.get<AgingReportDto[]>(`${this.endpoint}/ar-aging`, asOf ? { asOf } : undefined);
  }

  apAging(asOf?: string): Observable<AgingReportDto[]> {
    return this.api.get<AgingReportDto[]>(`${this.endpoint}/ap-aging`, asOf ? { asOf } : undefined);
  }

  downloadArAgingPdf(asOf?: string): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/ar-aging/pdf`, asOf ? { asOf } : undefined);
  }

  downloadApAgingPdf(asOf?: string): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/ap-aging/pdf`, asOf ? { asOf } : undefined);
  }

  // ---- Trading activity ----

  salesSummary(range?: DateRange): Observable<SalesSummaryDto[]> {
    return this.api.get<SalesSummaryDto[]>(`${this.endpoint}/sales-summary`, this.rangeParams(range));
  }

  purchaseSummary(range?: DateRange): Observable<PurchaseSummaryDto[]> {
    return this.api.get<PurchaseSummaryDto[]>(`${this.endpoint}/purchase-summary`, this.rangeParams(range));
  }

  profitability(range?: DateRange): Observable<ProfitabilityDto[]> {
    return this.api.get<ProfitabilityDto[]>(`${this.endpoint}/profitability`, this.rangeParams(range));
  }

  downloadSalesSummaryPdf(range?: DateRange): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/sales-summary/pdf`, this.rangeParams(range));
  }

  downloadPurchaseSummaryPdf(range?: DateRange): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/purchase-summary/pdf`, this.rangeParams(range));
  }

  downloadProfitabilityPdf(range?: DateRange): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/profitability/pdf`, this.rangeParams(range));
  }

  /** Empty values are dropped by ApiService, so an unset bound is simply omitted. */
  private rangeParams(range?: DateRange): Record<string, string | number | boolean> {
    const params: Record<string, string | number | boolean> = {};
    if (range?.startDate) params['startDate'] = range.startDate;
    if (range?.endDate) params['endDate'] = range.endDate;
    return params;
  }
}