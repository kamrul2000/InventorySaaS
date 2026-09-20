export interface PickLineDto {
  salesOrderItemId: string;
  productId: string;
  productName: string;
  productSku: string;
  barcode?: string;
  trackBatch: boolean;
  trackSerial: boolean;
  /** How much still needs picking on this line, i.e. ordered minus already delivered. */
  requestedQuantity: number;
  pickedQuantity: number;
  remainingQuantity: number;
  deliveredQuantity: number;
}

export interface PickSessionDto {
  id: string;
  salesOrderId: string;
  orderNumber: string;
  orderStatus: string;
  customerName: string;
  warehouseId: string;
  warehouseName: string;
  status: 'Open' | 'Completed' | 'Cancelled';
  startedAt: string;
  completedAt?: string;
  pickedBy?: string;
  notes?: string;
  totalRequested: number;
  totalPicked: number;
  totalRemaining: number;
  isFullyPicked: boolean;
  lines: PickLineDto[];
}

export interface PickScanResultDto {
  accepted: boolean;
  message: string;
  line?: PickLineDto;
  session: PickSessionDto;
}
