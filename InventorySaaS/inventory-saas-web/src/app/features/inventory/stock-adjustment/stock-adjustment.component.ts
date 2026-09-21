import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { SearchableSelectModule } from '../../../shared/searchable-select/searchable-select.module';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { InventoryService } from '../../../core/services/inventory.service';
import { ProductService } from '../../../core/services/product.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ProductDto, WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';

/** Preset reasons, so the ledger holds comparable values instead of free-form prose. */
const ADJUSTMENT_REASONS = [
  'Physical count correction',
  'Damaged goods',
  'Expired stock written off',
  'Lost or stolen',
  'Data entry error',
  'Opening balance correction',
];

@Component({
  selector: 'app-stock-adjustment',
  standalone: true,
  imports: [
    SearchableSelectModule,
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatIconModule,
  ],
  templateUrl: './stock-adjustment.component.html',
  styleUrl: './stock-adjustment.component.css',
})
export class StockAdjustmentComponent implements OnInit {
  form: FormGroup;
  products: ProductDto[] = [];
  warehouses: WarehouseDto[] = [];
  locations: WarehouseLocationDto[] = [];
  readonly reasons = ADJUSTMENT_REASONS;
  saving = false;

  /** Quantity the system currently believes is on hand, or null while unknown. */
  systemQuantity: number | null = null;
  loadingSystemQuantity = false;

  constructor(
    private fb: FormBuilder,
    private router: Router,
    private inventoryService: InventoryService,
    private productService: ProductService,
    private warehouseService: WarehouseService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      productId: ['', [Validators.required]],
      warehouseId: ['', [Validators.required]],
      locationId: [''],
      newQuantity: [0, [Validators.required, Validators.min(0)]],
      reason: ['', [Validators.required]],
      reasonDetail: [''],
    });
  }

  ngOnInit(): void {
    this.searchProducts('');
    this.searchWarehouses('');
  }

  searchProducts(search: string): void {
    this.productService.getAll({ pageSize: 100, search }).subscribe({
      next: (result) => this.products = result.items,
    });
  }

  searchWarehouses(search: string): void {
    this.warehouseService.getAll({ pageSize: 100, search }).subscribe({
      next: (result) => this.warehouses = result.items,
    });
  }

  onWarehouseChange(warehouseId: string): void {
    this.locations = [];
    this.form.patchValue({ locationId: '' });
    if (warehouseId) {
      this.warehouseService.getLocations(warehouseId).subscribe({
        next: (locs) => this.locations = locs,
      });
    }
    this.refreshSystemQuantity();
  }

  /**
   * The API takes an absolute new quantity, so the counted figure only makes sense next to what
   * the system holds today. Seeds the counted field with it, leaving the variance at zero.
   */
  refreshSystemQuantity(): void {
    const { productId, warehouseId, locationId } = this.form.value;
    if (!productId || !warehouseId) {
      this.systemQuantity = null;
      return;
    }

    this.loadingSystemQuantity = true;
    this.inventoryService.getBalances({ productId, warehouseId, pageSize: 100 }).subscribe({
      next: (result) => {
        const matches = result.items.filter((b) => (b.locationId ?? null) === (locationId || null));
        this.systemQuantity = matches.reduce((sum, b) => sum + b.quantityOnHand, 0);
        this.form.patchValue({ newQuantity: this.systemQuantity });
        this.loadingSystemQuantity = false;
      },
      error: () => {
        this.systemQuantity = null;
        this.loadingSystemQuantity = false;
      },
    });
  }

  get variance(): number | null {
    if (this.systemQuantity === null) return null;
    const counted = Number(this.form.value.newQuantity);
    if (Number.isNaN(counted)) return null;
    return counted - this.systemQuantity;
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const { productId, warehouseId, locationId, newQuantity, reason, reasonDetail } = this.form.value;
    const fullReason = reasonDetail?.trim() ? `${reason} — ${reasonDetail.trim()}` : reason;

    this.inventoryService.adjustment({
      productId,
      warehouseId,
      locationId: locationId || null,
      newQuantity: Number(newQuantity),
      reason: fullReason,
    }).subscribe({
      next: () => {
        this.notification.success('Stock adjustment recorded successfully');
        this.router.navigate(['/inventory']);
      },
      error: () => { this.saving = false; },
    });
  }
}
