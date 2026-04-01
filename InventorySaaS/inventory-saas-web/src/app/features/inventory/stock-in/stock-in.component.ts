import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
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
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatDatepickerModule,
    MatNativeDateModule,
  ],
  template: `
    <h2 mat-dialog-title>Stock In</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Product</mat-label>
          <mat-select formControlName="productId">
            @for (p of products; track p.id) {
              <mat-option [value]="p.id">{{ p.name }} ({{ p.sku }})</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Warehouse</mat-label>
          <mat-select formControlName="warehouseId" (selectionChange)="onWarehouseChange($event.value)">
            @for (wh of warehouses; track wh.id) {
              <mat-option [value]="wh.id">{{ wh.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (locations.length > 0) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Location</mat-label>
            <mat-select formControlName="locationId">
              <mat-option value="">-- None --</mat-option>
              @for (loc of locations; track loc.id) {
                <mat-option [value]="loc.id">{{ loc.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }

        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Quantity</mat-label>
            <input matInput formControlName="quantity" type="number">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Unit Cost</mat-label>
            <input matInput formControlName="unitCost" type="number" step="0.01">
          </mat-form-field>
        </div>

        <div class="form-row">
          <mat-form-field appearance="outline">
            <mat-label>Batch Number</mat-label>
            <input matInput formControlName="batchNumber">
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Lot Number</mat-label>
            <input matInput formControlName="lotNumber">
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Expiry Date</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="expiryDate">
          <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
          <mat-datepicker #picker></mat-datepicker>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Notes</mat-label>
          <textarea matInput formControlName="notes" rows="2"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button color="primary" (click)="save()" [disabled]="form.invalid || saving">
        Stock In
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
    .form-row { display: flex; gap: 16px; }
    .form-row mat-form-field { flex: 1; }
    mat-dialog-content { min-width: 500px; }
  `],
})
export class StockInComponent implements OnInit {
  form: FormGroup;
  products: ProductDto[] = [];
  warehouses: WarehouseDto[] = [];
  locations: WarehouseLocationDto[] = [];
  saving = false;

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<StockInComponent>,
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
        this.dialogRef.close(true);
      },
      error: () => { this.saving = false; },
    });
  }
}
