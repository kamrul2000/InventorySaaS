import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { CategoryDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly endpoint = '/api/v1/categories';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
  }): Observable<PaginatedList<CategoryDto>> {
    return this.api.getList<CategoryDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<CategoryDto> {
    return this.api.get<CategoryDto>(`${this.endpoint}/${id}`);
  }

  create(category: Partial<CategoryDto>): Observable<CategoryDto> {
    return this.api.post<CategoryDto>(this.endpoint, category);
  }

  update(id: string, category: Partial<CategoryDto>): Observable<CategoryDto> {
    return this.api.put<CategoryDto>(`${this.endpoint}/${id}`, category);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }
}
