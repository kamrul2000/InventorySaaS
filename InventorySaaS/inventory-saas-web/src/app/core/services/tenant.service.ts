import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { TenantDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class TenantService {
  private readonly endpoint = '/api/v1/tenants';

  constructor(private api: ApiService) {}

  getCurrent(): Observable<TenantDto> {
    return this.api.get<TenantDto>(`${this.endpoint}/current`);
  }

  update(tenant: Partial<TenantDto>): Observable<TenantDto> {
    return this.api.put<TenantDto>(`${this.endpoint}/current`, tenant);
  }

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
  }): Observable<PaginatedList<TenantDto>> {
    return this.api.getList<TenantDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }
}
