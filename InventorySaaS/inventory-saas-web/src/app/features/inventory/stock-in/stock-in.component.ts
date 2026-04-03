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
  selector: 'app-stock-in',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    MatIconModule,
  ],
  templateUrl: './stock-in.component.html',
  styleUrl: './stock-in.component.css',
})
export class StockInComponent implements OnInit {
  form: FormGroup;
  products: ProductDto[] = [];
  warehouses: WarehouseDto[] = [];
  locations: WarehouseLocationDto[] = [];
  saving = false;

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
      unitCost: [0, [Validators.required, Validators.min(0)]],
      batchNumber: [''],
      lotNumber: [''],
      expiryDate: [null],
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
    this.warehouseService.getLocations(warehouseId).subscribe({
      next: (locs) => this.locations = locs,
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const data = { ...this.form.value };
    if (!data.locationId) data.locationId = null;
    if (!data.batchNumber) data.batchNumber = null;
    if (!data.lotNumber) data.lotNumber = null;
    if (!data.notes) data.notes = null;
    if (data.expiryDate) {
      data.expiryDate = new Date(data.expiryDate).toISOString();
    }

    this.inventoryService.stockIn(data).subscribe({
      next: () => {
        this.notification.success('Stock in recorded successfully');
        this.router.navigate(['/inventory']);
      },
      error: () => { this.saving = false; },
    });
  }
}
