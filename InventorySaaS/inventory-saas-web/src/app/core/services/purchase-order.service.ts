import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { PurchaseOrderDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class PurchaseOrderService {
  private readonly endpoint = '/api/v1/purchaseorders';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
    supplierId?: string;
    sortBy?: string;
    sortDirection?: string;
  }): Observable<PaginatedList<PurchaseOrderDto>> {
    return this.api.getList<PurchaseOrderDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<PurchaseOrderDto> {
    return this.api.get<PurchaseOrderDto>(`${this.endpoint}/${id}`);
  }

  create(order: unknown): Observable<PurchaseOrderDto> {
    return this.api.post<PurchaseOrderDto>(this.endpoint, order);
  }

  approve(id: string): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/${id}/approve`, {});
  }

  receiveGoods(id: string, data?: unknown): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/${id}/receive`, data || {});
  }
}
