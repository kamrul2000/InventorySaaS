import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PickScanResultDto, PickSessionDto } from '../models/picking.models';

@Injectable({ providedIn: 'root' })
export class PickingService {
  constructor(private api: ApiService) {}

  private base(salesOrderId: string): string {
    return `/api/v1/sales-orders/${salesOrderId}/picking`;
  }

  /** Returns the open session, or null when the API answers 204 (nothing in progress). */
  get(salesOrderId: string): Observable<PickSessionDto | null> {
    return this.api.get<PickSessionDto | null>(this.base(salesOrderId));
  }

  start(salesOrderId: string, notes?: string): Observable<PickSessionDto> {
    return this.api.post<PickSessionDto>(`${this.base(salesOrderId)}/start`, { notes: notes ?? null });
  }

  /**
   * Records one pick. `idempotencyKey` must be regenerated per distinct scan and reused only
   * when retrying that same scan, so a dropped response cannot count the item twice.
   */
  scan(
    salesOrderId: string,
    data: {
      productId?: string;
      barcode?: string;
      quantity?: number;
      batchNumber?: string | null;
      serialNumber?: string | null;
    },
    idempotencyKey: string
  ): Observable<PickScanResultDto> {
    return this.api.post<PickScanResultDto>(`${this.base(salesOrderId)}/scan`, data, {
      'Idempotency-Key': idempotencyKey,
    });
  }

  complete(salesOrderId: string, deliver: boolean, notes?: string): Observable<PickSessionDto> {
    return this.api.post<PickSessionDto>(`${this.base(salesOrderId)}/complete`, {
      deliver,
      notes: notes ?? null,
    });
  }

  cancel(salesOrderId: string): Observable<PickSessionDto> {
    return this.api.post<PickSessionDto>(`${this.base(salesOrderId)}/cancel`, {});
  }
}
