import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiService } from './api.service';
import { ScanAvailabilityDto, ScanKind, ScanResultDto } from '../models/scan.models';

@Injectable({ providedIn: 'root' })
export class ScanService {
  private readonly endpoint = '/api/v1/scan';

  constructor(private api: ApiService) {}

  /**
   * Identifies what a scanned string points at. Pass `expectedKinds` when the screen only
   * accepts certain targets — a bin prompt should not silently take a product barcode.
   */
  resolve(rawValue: string, expectedKinds?: ScanKind[]): Observable<ScanResultDto> {
    return this.api.post<ScanResultDto>(`${this.endpoint}/resolve`, { rawValue, expectedKinds });
  }

  /** Scan-to-search: a product with its stock by warehouse, location, batch and serial. */
  findProduct(code: string): Observable<ScanResultDto> {
    return this.api.get<ScanResultDto>(`${this.endpoint}/product`, { code });
  }

  /** Stock for one product/warehouse/location/batch, for the pre-submit availability check. */
  getAvailability(params: {
    productId: string;
    warehouseId: string;
    locationId?: string;
    batchNumber?: string;
  }): Observable<ScanAvailabilityDto> {
    return this.api.get<ScanAvailabilityDto>(
      `${this.endpoint}/availability`,
      params as Record<string, string>
    );
  }
}
