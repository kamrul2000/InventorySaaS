import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { BrandDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class BrandService {
  private readonly endpoint = '/api/v1/brands';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    /** Bound by the API as `search` — the name has to match the controller's query parameter. */
    search?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }): Observable<PaginatedList<BrandDto>> {
    return this.api.getList<BrandDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<BrandDto> {
    return this.api.get<BrandDto>(`${this.endpoint}/${id}`);
  }

  create(brand: Partial<BrandDto>): Observable<BrandDto> {
    return this.api.post<BrandDto>(this.endpoint, brand);
  }

  update(id: string, brand: Partial<BrandDto>): Observable<BrandDto> {
    return this.api.put<BrandDto>(`${this.endpoint}/${id}`, brand);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }
}
