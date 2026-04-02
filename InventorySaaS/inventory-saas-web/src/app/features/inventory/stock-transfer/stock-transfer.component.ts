import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { InventoryService } from '../../../core/services/inventory.service';
import { ProductService } from '../../../core/services/product.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { ProductDto, WarehouseDto, WarehouseLocationDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-stock-transfer',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  templateUrl: './stock-transfer.component.html',
  styleUrl: './stock-transfer.component.css',
})
export class StockTransferComponent implements OnInit {
  form: FormGroup;
  products: ProductDto[] = [];
  warehouses: WarehouseDto[] = [];
  sourceLocations: WarehouseLocationDto[] = [];
  destLocations: WarehouseLocationDto[] = [];
  saving = false;

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<StockTransferComponent>,
    private inventoryService: InventoryService,
    private productService: ProductService,
    private warehouseService: WarehouseService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      productId: ['', [Validators.required]],
      sourceWarehouseId: ['', [Validators.required]],
      sourceLocationId: [''],
      destinationWarehouseId: ['', [Validators.required]],
      destinationLocationId: [''],
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

  onSourceWarehouseChange(warehouseId: string): void {
    this.warehouseService.getLocations(warehouseId).subscribe({
      next: (locs) => this.sourceLocations = locs,
    });
  }

  onDestWarehouseChange(warehouseId: string): void {
    this.warehouseService.getLocations(warehouseId).subscribe({
      next: (locs) => this.destLocations = locs,
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.inventoryService.transfer(this.form.value).subscribe({
      next: () => {
        this.notification.success('Transfer completed successfully');
        this.dialogRef.close(true);
      },
      error: () => { this.saving = false; },
    });
  }
}
