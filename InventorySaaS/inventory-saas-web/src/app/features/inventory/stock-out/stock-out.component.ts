import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { InventoryService } from '../../../core/services/inventory.service';
import { ProductService } from '../../../core/services/product.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ProductDto, WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-stock-out',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatIconModule,
  ],
  templateUrl: './stock-out.component.html',
  styleUrl: './stock-out.component.css',
})
export class StockOutComponent implements OnInit {
  form: FormGroup;
  products: ProductDto[] = [];
  warehouses: WarehouseDto[] = [];
  locations: WarehouseLocationDto[] = [];
  saving = false;

  /** Available quantity at the selected product/warehouse/location, or null while unknown. */
  availableQuantity: number | null = null;
  checkingAvailability = false;

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
      quantity: [1, [Validators.required, Validators.min(1)]],
      notes: [''],
    });
  }

  ngOnInit(): void {
    this.productService.getAll({ pageSize: 200 }).subscribe({
      next: (result) => this.products = result.items,
    });
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({
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
    this.refreshAvailability();
  }

  /**
   * Shows what the API will check before it rejects the submission. The balance row is keyed by
   * (product, warehouse, location), so an empty location is matched against balances with no location.
   */
  refreshAvailability(): void {
    const { productId, warehouseId, locationId } = this.form.value;
    if (!productId || !warehouseId) {
      this.availableQuantity = null;
      return;
    }

    this.checkingAvailability = true;
    this.inventoryService.getBalances({ productId, warehouseId, pageSize: 100 }).subscribe({
      next: (result) => {
        const matches = result.items.filter((b) => (b.locationId ?? null) === (locationId || null));
        this.availableQuantity = matches.reduce((sum, b) => sum + b.quantityAvailable, 0);
        this.checkingAvailability = false;
      },
      error: () => {
        this.availableQuantity = null;
        this.checkingAvailability = false;
      },
    });
  }

  get exceedsAvailable(): boolean {
    return this.availableQuantity !== null && this.form.value.quantity > this.availableQuantity;
  }

  save(): void {
    if (this.form.invalid || this.exceedsAvailable) return;
    this.saving = true;

    const data = { ...this.form.value };
    if (!data.locationId) data.locationId = null;
    if (!data.notes) data.notes = null;

    this.inventoryService.stockOut(data).subscribe({
      next: () => {
        this.notification.success('Stock out recorded successfully');
        this.router.navigate(['/inventory']);
      },
      error: () => { this.saving = false; },
    });
  }
}
