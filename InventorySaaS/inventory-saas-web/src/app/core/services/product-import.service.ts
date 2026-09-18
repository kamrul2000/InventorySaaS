import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { ProductImportResult } from '../models/domain.models';
import { environment } from '../../../environments/environment';

@Injectable({ providedIn: 'root' })
export class ProductImportService {
  private readonly endpoint = '/api/v1/products';

  constructor(private api: ApiService, private http: HttpClient) {}

  /** Validates the file and reports what would happen. Persists nothing. */
  preview(file: File, createMissingMasters: boolean): Observable<ProductImportResult> {
    return this.post('import/preview', file, createMissingMasters);
  }

  /** Imports every valid row; invalid rows are reported and skipped. */
  import(file: File, createMissingMasters: boolean): Observable<ProductImportResult> {
    return this.post('import', file, createMissingMasters);
  }

  downloadTemplate(): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/import/template`);
  }

  exportProducts(): Observable<Blob> {
    return this.api.getBlob(`${this.endpoint}/export`);
  }

  private post(path: string, file: File, createMissingMasters: boolean): Observable<ProductImportResult> {
    const formData = new FormData();
    formData.append('file', file, file.name);

    return this.http.post<ProductImportResult>(
      `${environment.apiUrl}${this.endpoint}/${path}?createMissingMasters=${createMissingMasters}`,
      formData
    );
  }
}
