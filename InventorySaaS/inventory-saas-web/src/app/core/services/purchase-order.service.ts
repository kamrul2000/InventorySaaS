import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { PurchaseOrderDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class PurchaseOrderService {
  private readonly endpoint = '/api/v1/PurchaseOrders';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    /** Bound by the API as `search` — the name has to match the controller's query parameter. */
    search?: string;
    status?: string;
    supplierId?: string;
    sortBy?: string;
    /** The API binds a boolean `sortDescending`, not a direction string. */
    sortDescending?: boolean;
  }): Observable<PaginatedList<PurchaseOrderDto>> {
    return this.api.getList<PurchaseOrderDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<PurchaseOrderDto> {
    return this.api.get<PurchaseOrderDto>(`${this.endpoint}/${id}`);
  }

  create(order: unknown): Observable<PurchaseOrderDto> {
    return this.api.post<PurchaseOrderDto>(this.endpoint, order);
  }

  approve(id: string): Observable<PurchaseOrderDto> {
    return this.api.post<PurchaseOrderDto>(`${this.endpoint}/${id}/approve`, {});
  }

  cancel(id: string): Observable<PurchaseOrderDto> {
    return this.api.post<PurchaseOrderDto>(`${this.endpoint}/${id}/cancel`, {});
  }

  receiveGoods(id: string, data?: unknown): Observable<PurchaseOrderDto> {
    return this.api.post<PurchaseOrderDto>(`${this.endpoint}/${id}/receive`, data || {});
  }

  returnGoods(id: string, data: unknown): Observable<PurchaseOrderDto> {
    return this.api.post<PurchaseOrderDto>(`${this.endpoint}/${id}/return`, data);
  }
}
