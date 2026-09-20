export type StockCountStatus = 'Counting' | 'PendingApproval' | 'Approved' | 'Cancelled';

export interface StockCountLineDto {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  barcode?: string;
  locationId?: string;
  locationName?: string;
  batchNumber?: string;
  trackSerial: boolean;
  /** What the system believed when this line was first opened. */
  systemQuantity: number;
  countedQuantity: number;
  /** Positive is an overage, negative a shortage. */
  variance: number;
  notes?: string;
  serials: string[];
}

export interface StockCountSessionDto {
  id: string;
  countNumber: string;
  warehouseId: string;
  warehouseName: string;
  locationId?: string;
  locationName?: string;
  status: StockCountStatus;
  startedAt: string;
  submittedAt?: string;
  approvedAt?: string;
  approvedBy?: string;
  notes?: string;
  lineCount: number;
  shortageLines: number;
  overageLines: number;
  netVariance: number;
  lines: StockCountLineDto[];
}

export interface StockCountScanResultDto {
  accepted: boolean;
  message: string;
  line?: StockCountLineDto;
  session: StockCountSessionDto;
}
