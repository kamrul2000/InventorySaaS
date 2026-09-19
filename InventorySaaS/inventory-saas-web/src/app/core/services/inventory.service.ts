import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { InventoryBalanceDto, InventoryTransactionDto } from '../models/domain.models';

/** Fields every scan-driven write accepts on top of its own payload. */
interface ScanWriteOptions {
  /**
   * Sent as `Idempotency-Key`. Generate one per user-initiated operation and reuse it across
   * retries, so a double-tap or a dropped response cannot post the movement twice.
   */
  idempotencyKey?: string;
}

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly endpoint = '/api/v1/inventory';

  constructor(private api: ApiService) {}

  /** A key unique to one operation attempt, safe to reuse when retrying that same attempt. */
  static newIdempotencyKey(): string {
    return crypto.randomUUID();
  }

  getBalances(params?: {
    pageNumber?: number;
    pageSize?: number;
    warehouseId?: string;
    productId?: string;
    /** Bound by the API as `search` — the name has to match the controller's query parameter. */
    search?: string;
  }): Observable<PaginatedList<InventoryBalanceDto>> {
    return this.api.getList<InventoryBalanceDto>(`${this.endpoint}/balances`, params as Record<string, string | number | boolean>);
  }

  getTransactions(params?: {
    pageNumber?: number;
    pageSize?: number;
    warehouseId?: string;
    productId?: string;
    transactionType?: string;
    startDate?: string;
    endDate?: string;
  }): Observable<PaginatedList<InventoryTransactionDto>> {
    return this.api.getList<InventoryTransactionDto>(`${this.endpoint}/transactions`, params as Record<string, string | number | boolean>);
  }

  stockIn(
    data: {
      productId: string;
      warehouseId: string;
      locationId?: string | null;
      quantity: number;
      unitCost: number;
      batchNumber?: string | null;
      lotNumber?: string | null;
      expiryDate?: string | null;
      notes?: string | null;
      /** Required, and must match `quantity`, for serial-tracked products. */
      serialNumbers?: string[] | null;
    },
    options?: ScanWriteOptions
  ): Observable<InventoryTransactionDto> {
    return this.api.post<InventoryTransactionDto>(
      `${this.endpoint}/stock-in`,
      data,
      this.scanHeaders(options)
    );
  }

  stockOut(
    data: {
      productId: string;
      warehouseId: string;
      locationId?: string | null;
      quantity: number;
      notes?: string | null;
      reason?: string | null;
      /** Required for batch-tracked products so the batch is preserved on the way out. */
      batchNumber?: string | null;
      serialNumbers?: string[] | null;
    },
    options?: ScanWriteOptions
  ): Observable<InventoryTransactionDto> {
    return this.api.post<InventoryTransactionDto>(
      `${this.endpoint}/stock-out`,
      data,
      this.scanHeaders(options)
    );
  }

  transfer(
    data: {
      productId: string;
      sourceWarehouseId: string;
      sourceLocationId?: string | null;
      destinationWarehouseId: string;
      destinationLocationId?: string | null;
      quantity: number;
      notes?: string | null;
      batchNumber?: string | null;
      serialNumbers?: string[] | null;
    },
    options?: ScanWriteOptions
  ): Observable<InventoryTransactionDto> {
    return this.api.post<InventoryTransactionDto>(
      `${this.endpoint}/transfer`,
      data,
      this.scanHeaders(options)
    );
  }

  adjustment(data: {
    productId: string;
    warehouseId: string;
    locationId?: string | null;
    newQuantity: number;
    /** The API folds this into the transaction note — it does not take a separate notes field. */
    reason: string;
  }): Observable<InventoryTransactionDto> {
    return this.api.post<InventoryTransactionDto>(`${this.endpoint}/adjustment`, data);
  }

  private scanHeaders(options?: ScanWriteOptions): Record<string, string> | undefined {
    return options?.idempotencyKey ? { 'Idempotency-Key': options.idempotencyKey } : undefined;
  }
}
