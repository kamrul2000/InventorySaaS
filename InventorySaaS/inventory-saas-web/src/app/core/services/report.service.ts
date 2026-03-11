import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import {
  ExpiryReportDto,
  InventoryValuationDto,
  LowStockReportDto,
  StockSummaryReportDto,
} from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class ReportService {
  private readonly endpoint = '/api/v1/reports';

  constructor(private api: ApiService) {}

  stockSummary(params?: {
    warehouseId?: string;
    categoryId?: string;
  }): Observable<StockSummaryReportDto[]> {
    return this.api.get<StockSummaryReportDto[]>(`${this.endpoint}/stock-summary`, params as Record<string, string | number | boolean>);
  }

  lowStock(params?: {
    warehouseId?: string;
  }): Observable<LowStockReportDto[]> {
    return this.api.get<LowStockReportDto[]>(`${this.endpoint}/low-stock`, params as Record<string, string | number | boolean>);
  }

  expiry(params?: {
    warehouseId?: string;
    daysAhead?: number;
  }): Observable<ExpiryReportDto[]> {
    return this.api.get<ExpiryReportDto[]>(`${this.endpoint}/expiry`, params as Record<string, string | number | boolean>);
  }

  inventoryValuation(params?: {
    warehouseId?: string;
  }): Observable<InventoryValuationDto[]> {
    return this.api.get<InventoryValuationDto[]>(`${this.endpoint}/valuation`, params as Record<string, string | number | boolean>);
  }
}
