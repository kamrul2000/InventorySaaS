import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { PaymentDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class PaymentService {
  private readonly endpoint = '/api/v1/Payments';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    customerId?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }): Observable<PaginatedList<PaymentDto>> {
    return this.api.getList<PaymentDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<PaymentDto> {
    return this.api.get<PaymentDto>(`${this.endpoint}/${id}`);
  }

  create(payment: unknown): Observable<PaymentDto> {
    return this.api.post<PaymentDto>(this.endpoint, payment);
  }
}
