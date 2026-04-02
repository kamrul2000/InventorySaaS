import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { NotificationDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class NotificationApiService {
  private readonly endpoint = '/api/v1/notifications';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    isRead?: boolean;
  }): Observable<PaginatedList<NotificationDto>> {
    const queryParams: Record<string, string | number | boolean> = {};
    if (params?.pageNumber != null) queryParams['pageNumber'] = params.pageNumber;
    if (params?.pageSize != null) queryParams['pageSize'] = params.pageSize;
    if (params?.isRead === false) queryParams['unreadOnly'] = true;
    return this.api.getList<NotificationDto>(this.endpoint, queryParams);
  }

  markRead(id: string): Observable<void> {
    return this.api.put<void>(`${this.endpoint}/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.api.put<void>(`${this.endpoint}/read-all`, {});
  }
}
