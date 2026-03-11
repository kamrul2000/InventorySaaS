import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { ProductDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly endpoint = '/api/v1/products';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    categoryId?: string;
    brandId?: string;
    isActive?: boolean;
    sortBy?: string;
    sortDirection?: string;
  }): Observable<PaginatedList<ProductDto>> {
    return this.api.getList<ProductDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<ProductDto> {
    return this.api.get<ProductDto>(`${this.endpoint}/${id}`);
  }

  create(product: Partial<ProductDto>): Observable<ProductDto> {
    return this.api.post<ProductDto>(this.endpoint, product);
  }

  update(id: string, product: Partial<ProductDto>): Observable<ProductDto> {
    return this.api.put<ProductDto>(`${this.endpoint}/${id}`, product);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }
}
