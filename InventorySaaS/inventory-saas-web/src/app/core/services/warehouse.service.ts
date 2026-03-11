import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { WarehouseDto, WarehouseLocationDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class WarehouseService {
  private readonly endpoint = '/api/v1/warehouses';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
  }): Observable<PaginatedList<WarehouseDto>> {
    return this.api.getList<WarehouseDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<WarehouseDto> {
    return this.api.get<WarehouseDto>(`${this.endpoint}/${id}`);
  }

  create(warehouse: Partial<WarehouseDto>): Observable<WarehouseDto> {
    return this.api.post<WarehouseDto>(this.endpoint, warehouse);
  }

  update(id: string, warehouse: Partial<WarehouseDto>): Observable<WarehouseDto> {
    return this.api.put<WarehouseDto>(`${this.endpoint}/${id}`, warehouse);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }

  getLocations(warehouseId: string): Observable<WarehouseLocationDto[]> {
    return this.api.get<WarehouseLocationDto[]>(`${this.endpoint}/${warehouseId}/locations`);
  }

  createLocation(warehouseId: string, location: Partial<WarehouseLocationDto>): Observable<WarehouseLocationDto> {
    return this.api.post<WarehouseLocationDto>(`${this.endpoint}/${warehouseId}/locations`, location);
  }
}
