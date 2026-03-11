import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { SupplierDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class SupplierService {
  private readonly endpoint = '/api/v1/suppliers';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
  }): Observable<PaginatedList<SupplierDto>> {
    return this.api.getList<SupplierDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<SupplierDto> {
    return this.api.get<SupplierDto>(`${this.endpoint}/${id}`);
  }

  create(supplier: Partial<SupplierDto>): Observable<SupplierDto> {
    return this.api.post<SupplierDto>(this.endpoint, supplier);
  }

  update(id: string, supplier: Partial<SupplierDto>): Observable<SupplierDto> {
    return this.api.put<SupplierDto>(`${this.endpoint}/${id}`, supplier);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }
}
