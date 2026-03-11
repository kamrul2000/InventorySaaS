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
  template: `
    <h2 mat-dialog-title>Stock Transfer</h2>
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

        <h3>Source</h3>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Source Warehouse</mat-label>
          <mat-select formControlName="sourceWarehouseId" (selectionChange)="onSourceWarehouseChange($event.value)">
            @for (wh of warehouses; track wh.id) {
              <mat-option [value]="wh.id">{{ wh.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (sourceLocations.length > 0) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Source Location</mat-label>
            <mat-select formControlName="sourceLocationId">
              <mat-option value="">-- None --</mat-option>
              @for (loc of sourceLocations; track loc.id) {
                <mat-option [value]="loc.id">{{ loc.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }

        <h3>Destination</h3>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Destination Warehouse</mat-label>
          <mat-select formControlName="destinationWarehouseId" (selectionChange)="onDestWarehouseChange($event.value)">
            @for (wh of warehouses; track wh.id) {
              <mat-option [value]="wh.id">{{ wh.name }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        @if (destLocations.length > 0) {
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Destination Location</mat-label>
            <mat-select formControlName="destinationLocationId">
              <mat-option value="">-- None --</mat-option>
              @for (loc of destLocations; track loc.id) {
                <mat-option [value]="loc.id">{{ loc.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }

        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Quantity</mat-label>
          <input matInput formControlName="quantity" type="number">
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
        Transfer
      </button>
    </mat-dialog-actions>
  `,
  styles: [`
    .full-width { width: 100%; }
    mat-dialog-content { min-width: 500px; }
    h3 { margin: 16px 0 8px; color: rgba(0,0,0,.54); }
  `],
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
