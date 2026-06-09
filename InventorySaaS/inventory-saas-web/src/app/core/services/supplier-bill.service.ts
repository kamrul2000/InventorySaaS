import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { PaginatedList } from '../models/api.models';
import { SupplierBillDto, OutstandingBillDto } from '../models/domain.models';

@Injectable({ providedIn: 'root' })
export class SupplierBillService {
  private readonly endpoint = '/api/v1/SupplierBills';

  constructor(private api: ApiService) {}

  getAll(params?: {
    pageNumber?: number;
    pageSize?: number;
    searchTerm?: string;
    supplierId?: string;
    status?: string;
    sortBy?: string;
    sortDescending?: boolean;
  }): Observable<PaginatedList<SupplierBillDto>> {
    return this.api.getList<SupplierBillDto>(this.endpoint, params as Record<string, string | number | boolean>);
  }

  getById(id: string): Observable<SupplierBillDto> {
    return this.api.get<SupplierBillDto>(`${this.endpoint}/${id}`);
  }

  getOutstanding(supplierId: string): Observable<OutstandingBillDto[]> {
    return this.api.get<OutstandingBillDto[]>(`${this.endpoint}/outstanding/${supplierId}`);
  }

  create(bill: unknown): Observable<SupplierBillDto> {
    return this.api.post<SupplierBillDto>(this.endpoint, bill);
  }

  createFromPurchaseOrder(purchaseOrderId: string, supplierInvoiceNumber?: string | null, dueDate?: string | null): Observable<SupplierBillDto> {
    return this.api.post<SupplierBillDto>(`${this.endpoint}/from-purchase-order`, {
      purchaseOrderId, supplierInvoiceNumber: supplierInvoiceNumber ?? null, dueDate: dueDate ?? null,
    });
  }

  approve(id: string): Observable<SupplierBillDto> {
    return this.api.post<SupplierBillDto>(`${this.endpoint}/${id}/approve`, {});
  }

  cancel(id: string): Observable<SupplierBillDto> {
    return this.api.post<SupplierBillDto>(`${this.endpoint}/${id}/cancel`, {});
  }
}
