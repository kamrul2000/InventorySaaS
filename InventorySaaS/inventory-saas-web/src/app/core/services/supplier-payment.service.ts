import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { SupplierPaymentDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class SupplierPaymentService {
  private readonly endpoint = '/api/v1/SupplierPayments';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    supplierId?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }): Observable<PaginatedList<SupplierPaymentDto>> {
    return this.api.getList<SupplierPaymentDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<SupplierPaymentDto> {
    return this.api.get<SupplierPaymentDto>(`${this.endpoint}/${id}`);
  }

  create(payment: unknown): Observable<SupplierPaymentDto> {
    return this.api.post<SupplierPaymentDto>(this.endpoint, payment);
  }
}
