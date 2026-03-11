import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { CustomerDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly endpoint = '/api/v1/customers';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
  }): Observable<PaginatedList<CustomerDto>> {
    return this.api.getList<CustomerDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<CustomerDto> {
    return this.api.get<CustomerDto>(`${this.endpoint}/${id}`);
  }

  create(customer: Partial<CustomerDto>): Observable<CustomerDto> {
    return this.api.post<CustomerDto>(this.endpoint, customer);
  }

  update(id: string, customer: Partial<CustomerDto>): Observable<CustomerDto> {
    return this.api.put<CustomerDto>(`${this.endpoint}/${id}`, customer);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }
}
