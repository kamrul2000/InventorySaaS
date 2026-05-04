import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiService } from './api.service';
import {
  ExpiryReportDto,
  InventoryValuationDto,
  LowStockReportDto,
  StockSummaryReportDto,
} from '../models/domain.models';

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
}
