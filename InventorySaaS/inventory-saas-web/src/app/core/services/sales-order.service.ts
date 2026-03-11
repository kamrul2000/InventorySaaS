import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { SalesOrderDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class SalesOrderService {
  private readonly endpoint = '/api/v1/salesorders';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    status?: string;
    customerId?: string;
    sortBy?: string;
    sortDirection?: string;
  }): Observable<PaginatedList<SalesOrderDto>> {
    return this.api.getList<SalesOrderDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<SalesOrderDto> {
    return this.api.get<SalesOrderDto>(`${this.endpoint}/${id}`);
  }

  create(order: unknown): Observable<SalesOrderDto> {
    return this.api.post<SalesOrderDto>(this.endpoint, order);
  }

  confirm(id: string): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/${id}/confirm`, {});
  }

  deliver(id: string, data?: unknown): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/${id}/deliver`, data || {});
  }
}
