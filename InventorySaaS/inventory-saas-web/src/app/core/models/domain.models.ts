export interface TenantDto {
  id: string;
  name: string;
  slug: string;
  logoUrl?: string;
  contactEmail?: string;
  status: string;
  planName: string;
  createdAt: string;
}

export interface CategoryDto {
  id: string;
  name: string;
  description?: string;
  parentCategoryId?: string;
  isActive: boolean;
  productCount: number;
}

export interface BrandDto {
  id: string;
  name: string;
  description?: string;
  isActive: boolean;
}

export interface UnitOfMeasureDto {
  id: string;
  name: string;
  abbreviation: string;
  isActive: boolean;
}

export interface ProductDto {
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
  isActive: boolean;
  createdAt: string;
}

export interface WarehouseDto {
  id: string;
  name: string;
  code: string;
  address?: string;
  city?: string;
  isDefault: boolean;
  isActive: boolean;
  locationCount: number;
}

export interface WarehouseLocationDto {
  id: string;
  warehouseId: string;
  name: string;
  aisle?: string;
  rack?: string;
  bin?: string;
  isActive: boolean;
}

export interface InventoryBalanceDto {
  id: string;
  productId: string;
  productName: string;
  productSku: string;
  warehouseId: string;
  warehouseName: string;
  locationName?: string;
  batchNumber?: string;
  expiryDate?: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  unitCost: number;
}

export interface InventoryTransactionDto {
  id: string;
  transactionNumber: string;
  transactionType: string;
  productName: string;
  productSku: string;
  warehouseName: string;
  quantity: number;
  unitCost: number;
  batchNumber?: string;
  transactionDate: string;
  notes?: string;
}

export interface SupplierDto {
  id: string;
  name: string;
  code?: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  city?: string;
  country?: string;
  isActive: boolean;
}

export interface CustomerDto {
  id: string;
  name: string;
  code?: string;
  customerType?: string;
  contactPerson?: string;
  email?: string;
  phone?: string;
  city?: string;
  country?: string;
  isActive: boolean;
}

export interface PurchaseOrderDto {
  id: string;
  orderNumber: string;
  supplierName: string;
  warehouseName: string;
  orderDate: string;
  expectedDeliveryDate?: string;
  status: string;
  totalAmount: number;
  items: PurchaseOrderItemDto[];
}

export interface PurchaseOrderItemDto {
  id: string;
  productName: string;
  productSku: string;
  quantity: number;
  receivedQuantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface SalesOrderDto {
  id: string;
  orderNumber: string;
  customerName: string;
  warehouseName: string;
  orderDate: string;
  deliveryDate?: string;
  status: string;
  totalAmount: number;
  items: SalesOrderItemDto[];
}

export interface SalesOrderItemDto {
  id: string;
  productName: string;
  productSku: string;
  quantity: number;
  deliveredQuantity: number;
  unitPrice: number;
  lineTotal: number;
}

export interface NotificationDto {
  id: string;
  type: string;
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  actionUrl?: string;
}

export interface DashboardDto {
  totalProducts: number;
  totalWarehouses: number;
  totalSuppliers: number;
  totalCustomers: number;
  lowStockCount: number;
  expiringCount: number;
  totalInventoryValue: number;
  totalSales: number;
  totalPurchases: number;
  totalOrders: number;
  recentTransactions: RecentTransactionDto[];
  topProducts: TopProductDto[];
  stockAlerts: StockAlertDto[];
  recentSalesOrders: RecentSalesOrderDto[];
  lowStockProducts: LowStockProductDto[];
}

export interface RecentTransactionDto {
  transactionNumber: string;
  type: string;
  productName: string;
  quantity: number;
  date: string;
}

export interface TopProductDto {
  productName: string;
  sku: string;
  totalQuantity: number;
  totalValue: number;
  sellingPrice: number;
}

export interface StockAlertDto {
  productName: string;
  sku: string;
  warehouseName: string;
  currentStock: number;
  reorderLevel: number;
}

export interface RecentSalesOrderDto {
  orderNumber: string;
  customerName: string;
  status: string;
  totalAmount: number;
  orderDate: string;
}

export interface LowStockProductDto {
  productName: string;
  sku: string;
  currentStock: number;
  reorderLevel: number;
}

export interface StockSummaryReportDto {
  productName: string;
  sku: string;
  categoryName: string;
  warehouseName: string;
  quantityOnHand: number;
  unitCost: number;
  totalValue: number;
}

export interface LowStockReportDto {
  productName: string;
  sku: string;
  warehouseName: string;
  currentStock: number;
  reorderLevel: number;
  deficit: number;
}

export interface ExpiryReportDto {
  productName: string;
  sku: string;
  warehouseName: string;
  batchNumber?: string;
  expiryDate: string;
  quantity: number;
  daysUntilExpiry: number;
}

export interface InventoryValuationDto {
  categoryName: string;
  productCount: number;
  totalCostValue: number;
  totalSellingValue: number;
}
