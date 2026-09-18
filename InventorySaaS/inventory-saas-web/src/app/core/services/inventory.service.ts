import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { InventoryBalanceDto, InventoryTransactionDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class InventoryService {
  private readonly endpoint = '/api/v1/inventory';

  constructor(private api: ApiService) {}

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

  stockIn(data: {
    productId: string;
    warehouseId: string;
    locationId?: string;
    quantity: number;
    unitCost: number;
    batchNumber?: string;
    lotNumber?: string;
    expiryDate?: string;
    notes?: string;
  }): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/stock-in`, data);
  }

  stockOut(data: {
    productId: string;
    warehouseId: string;
    locationId?: string;
    quantity: number;
    notes?: string;
  }): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/stock-out`, data);
  }

  transfer(data: {
    productId: string;
    sourceWarehouseId: string;
    sourceLocationId?: string;
    destinationWarehouseId: string;
    destinationLocationId?: string;
    quantity: number;
    notes?: string;
  }): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/transfer`, data);
  }

  adjustment(data: {
    productId: string;
    warehouseId: string;
    locationId?: string | null;
    newQuantity: number;
    /** The API folds this into the transaction note — it does not take a separate notes field. */
    reason: string;
  }): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/adjustment`, data);
  }
}
