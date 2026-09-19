/** What a scanned string turned out to be. Mirrors the API's `ScanKind`. */
export type ScanKind =
  | 'Unknown'
  | 'Product'
  | 'ProductVariant'
  | 'Serial'
  | 'Location'
  | 'Warehouse'
  | 'SalesOrder'
  | 'PurchaseOrder';

/** How the raw value was decoded, before any lookup. */
export type ScanPayloadFormat = 'Plain' | 'Json' | 'Gs1';

/** Structured fields carried by the label itself. */
export interface ScanPayloadDto {
  code: string;
  format: ScanPayloadFormat;
  batchNumber?: string;
  serialNumber?: string;
  expiryDate?: string;
  quantity?: number;
}

/** One inventory balance row behind a scanned product. */
export interface ScannedStockDto {
  balanceId: string;
  warehouseId: string;
  warehouseName: string;
  locationId?: string;
  locationName?: string;
  locationCode?: string;
  batchNumber?: string;
  expiryDate?: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  unitCost: number;
}

export interface ScannedSerialDto {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  serialNumber: string;
  status: 'InStock' | 'Reserved' | 'Issued' | 'Scrapped';
  warehouseId?: string;
  warehouseName?: string;
  locationId?: string;
  locationName?: string;
  batchNumber?: string;
  expiryDate?: string;
}

export interface ScannedProductDto {
  id: string;
  name: string;
  sku: string;
  barcode?: string;
  categoryName: string;
  brandName?: string;
  unitName: string;
  costPrice: number;
  sellingPrice: number;
  reorderLevel: number;
  trackExpiry: boolean;
  trackBatch: boolean;
  trackSerial: boolean;
  isActive: boolean;
  /** Set when the scan matched a variant rather than the parent product. */
  variantId?: string;
  variantName?: string;
  totalOnHand: number;
  totalAvailable: number;
  stock: ScannedStockDto[];
  serials: ScannedSerialDto[];
}

export interface ScannedLocationDto {
  id: string;
  name: string;
  code?: string;
  barcode?: string;
  warehouseId: string;
  warehouseName: string;
  aisle?: string;
  rack?: string;
  bin?: string;
  isActive: boolean;
}

export interface ScannedWarehouseDto {
  id: string;
  name: string;
  code: string;
  isActive: boolean;
}

export interface ScannedDocumentDto {
  id: string;
  documentType: 'SalesOrder' | 'PurchaseOrder';
  number: string;
  status: string;
  warehouseId: string;
  warehouseName: string;
  partyName: string;
  orderDate: string;
  lineCount: number;
}

/**
 * The result of a scan. `matched: false` with `kind: 'Unknown'` is a normal outcome, not an
 * error — the UI shows `message` and stays ready for the next scan.
 */
export interface ScanResultDto {
  kind: ScanKind;
  matched: boolean;
  rawValue: string;
  payload: ScanPayloadDto;
  message?: string;
  product?: ScannedProductDto;
  location?: ScannedLocationDto;
  warehouse?: ScannedWarehouseDto;
  serial?: ScannedSerialDto;
  document?: ScannedDocumentDto;
}

export interface ScanAvailabilityDto {
  productId: string;
  productName: string;
  productSku: string;
  warehouseId: string;
  warehouseName: string;
  locationId?: string;
  locationName?: string;
  batchNumber?: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  unitCost: number;
}
