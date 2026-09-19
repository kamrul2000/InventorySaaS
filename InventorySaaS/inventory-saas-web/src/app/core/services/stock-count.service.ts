import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import {
  StockCountScanResultDto,
  StockCountSessionDto,
  StockCountStatus,
} from '../models/stock-count.models';

@Injectable({ providedIn: 'root' })
export class StockCountService {
  private readonly endpoint = '/api/v1/stock-counts';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    search?: string;
    warehouseId?: string;
    status?: StockCountStatus;
  }): Observable<PaginatedList<StockCountSessionDto>> {
    return this.api.getList<StockCountSessionDto>(
      this.endpoint,
      params as Record<string, string | number | boolean>
    );
  }

  getById(id: string): Observable<StockCountSessionDto> {
    return this.api.get<StockCountSessionDto>(`${this.endpoint}/${id}`);
  }

  start(data: {
    warehouseId: string;
    locationId?: string | null;
    notes?: string | null;
  }): Observable<StockCountSessionDto> {
    return this.api.post<StockCountSessionDto>(this.endpoint, data);
  }

  /**
   * Records a counted item. Repeat scans of the same product, bin and batch add up; pass
   * `setExactQuantity` to replace the running total with a typed correction instead.
   */
  scan(
    id: string,
    data: {
      productId?: string;
      barcode?: string;
      quantity?: number;
      locationId?: string | null;
      batchNumber?: string | null;
      serialNumber?: string | null;
      setExactQuantity?: boolean;
      notes?: string | null;
    },
    idempotencyKey: string
  ): Observable<StockCountScanResultDto> {
    return this.api.post<StockCountScanResultDto>(`${this.endpoint}/${id}/scan`, data, {
      'Idempotency-Key': idempotencyKey,
    });
  }

  submit(id: string, notes?: string): Observable<StockCountSessionDto> {
    return this.api.post<StockCountSessionDto>(`${this.endpoint}/${id}/submit`, {
      notes: notes ?? null,
    });
  }

  /** Manager only — this is the call that posts the adjustments. */
  approve(id: string, notes?: string): Observable<StockCountSessionDto> {
    return this.api.post<StockCountSessionDto>(`${this.endpoint}/${id}/approve`, {
      notes: notes ?? null,
    });
  }

  cancel(id: string): Observable<StockCountSessionDto> {
    return this.api.post<StockCountSessionDto>(`${this.endpoint}/${id}/cancel`, {});
  }
}
