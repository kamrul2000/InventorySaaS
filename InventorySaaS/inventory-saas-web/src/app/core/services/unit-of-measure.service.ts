import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { UnitOfMeasureDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class UnitOfMeasureService {
  private readonly endpoint = '/api/v1/unitsofmeasure';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    /** Bound by the API as `search` — the name has to match the controller's query parameter. */
    search?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }): Observable<PaginatedList<UnitOfMeasureDto>> {
    return this.api.getList<UnitOfMeasureDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<UnitOfMeasureDto> {
    return this.api.get<UnitOfMeasureDto>(`${this.endpoint}/${id}`);
  }

  create(unit: Partial<UnitOfMeasureDto>): Observable<UnitOfMeasureDto> {
    return this.api.post<UnitOfMeasureDto>(this.endpoint, unit);
  }

  update(id: string, unit: Partial<UnitOfMeasureDto>): Observable<UnitOfMeasureDto> {
    return this.api.put<UnitOfMeasureDto>(`${this.endpoint}/${id}`, unit);
  }

  delete(id: string): Observable<void> {
    return this.api.delete(`${this.endpoint}/${id}`);
  }
}
