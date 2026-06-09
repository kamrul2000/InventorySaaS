import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { InvoiceDto, OutstandingInvoiceDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class InvoiceService {
  private readonly endpoint = '/api/v1/Invoices';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    customerId?: string;
    status?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }): Observable<PaginatedList<InvoiceDto>> {
    return this.api.getList<InvoiceDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<InvoiceDto> {
    return this.api.get<InvoiceDto>(`${this.endpoint}/${id}`);
  }

  getOutstanding(customerId: string): Observable<OutstandingInvoiceDto[]> {
    return this.api.get<OutstandingInvoiceDto[]>(`${this.endpoint}/outstanding/${customerId}`);
  }

  create(invoice: unknown): Observable<InvoiceDto> {
    return this.api.post<InvoiceDto>(this.endpoint, invoice);
  }

  createFromSalesOrder(salesOrderId: string, dueDate?: string | null): Observable<InvoiceDto> {
    return this.api.post<InvoiceDto>(`${this.endpoint}/from-sales-order`, { salesOrderId, dueDate: dueDate ?? null });
  }

  issue(id: string): Observable<InvoiceDto> {
    return this.api.post<InvoiceDto>(`${this.endpoint}/${id}/issue`, {});
  }

  cancel(id: string): Observable<InvoiceDto> {
    return this.api.post<InvoiceDto>(`${this.endpoint}/${id}/cancel`, {});
  }
}
