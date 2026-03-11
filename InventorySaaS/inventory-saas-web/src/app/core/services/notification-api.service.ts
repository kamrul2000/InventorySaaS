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
    return this.api.getList<NotificationDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  markRead(id: string): Observable<void> {
    return this.api.put<void>(`${this.endpoint}/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/mark-all-read`, {});
  }
}
