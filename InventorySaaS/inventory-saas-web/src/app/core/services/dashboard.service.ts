import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { DashboardDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly endpoint = '/api/v1/dashboard';

  constructor(private api: ApiService) {}

  get(): Observable<DashboardDto> {
    return this.api.get<DashboardDto>(this.endpoint);
  }
}
