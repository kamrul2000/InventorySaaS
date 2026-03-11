import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { User } from '../models/auth.models';

@Injectable({ providedIn: 'root' })
export class UserService {
  private readonly endpoint = '/api/v1/users';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    isActive?: boolean;
  }): Observable<PaginatedList<User>> {
    return this.api.getList<User>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<User> {
    return this.api.get<User>(`${this.endpoint}/${id}`);
  }

  create(user: {
    email: string;
    firstName: string;
    lastName: string;
    phone?: string;
    password: string;
    roles: string[];
  }): Observable<User> {
    return this.api.post<User>(this.endpoint, user);
  }

  update(id: string, user: {
    firstName?: string;
    lastName?: string;
    phone?: string;
    isActive?: boolean;
    roles?: string[];
  }): Observable<User> {
    return this.api.put<User>(`${this.endpoint}/${id}`, user);
  }

  invite(email: string, roles: string[]): Observable<void> {
    return this.api.post<void>(`${this.endpoint}/invite`, { email, roles });
  }
}
