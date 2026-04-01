import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { SupplierService } from '../../../core/services/supplier.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { ProductService } from '../../../core/services/product.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SupplierDto, WarehouseDto, ProductDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-po-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatCardModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatButtonModule, MatIconModule,
    MatDatepickerModule, MatNativeDateModule, MatTableModule, MatProgressSpinnerModule,
  ],
  template: `
    <div class="form-container">
      <mat-card>
        <mat-card-header>
          <mat-card-title>Create Purchase Order</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Supplier</mat-label>
                <mat-select formControlName="supplierId">
                  @for (s of suppliers; track s.id) {
                    <mat-option [value]="s.id">{{ s.name }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Warehouse</mat-label>
                <mat-select formControlName="warehouseId">
                  @for (wh of warehouses; track wh.id) {
                    <mat-option [value]="wh.id">{{ wh.name }}</mat-option>
                  }
                </mat-select>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Expected Delivery Date</mat-label>
                <input matInput [matDatepicker]="picker" formControlName="expectedDeliveryDate">
                <mat-datepicker-toggle matSuffix [for]="picker"></mat-datepicker-toggle>
                <mat-datepicker #picker></mat-datepicker>
              </mat-form-field>
            </div>

            <h3>
              Line Items
              <button mat-mini-fab color="primary" type="button" (click)="addItem()">
                <mat-icon>add</mat-icon>
              </button>
            </h3>

            <div formArrayName="items">
              <table class="items-table full-width">
                <thead>
                  <tr>
                    <th>Product</th>
                    <th>Qty</th>
                    <th>Unit Price</th>
                    <th>Total</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  @for (item of items.controls; track $index) {
                    <tr [formGroupName]="$index">
                      <td>
                        <mat-select formControlName="productId" class="inline-select">
                          @for (p of products; track p.id) {
                            <mat-option [value]="p.id">{{ p.name }}</mat-option>
                          }
                        </mat-select>
                      </td>
                      <td>
                        <input matInput formControlName="quantity" type="number" class="inline-input" (input)="calculateLineTotal($index)">
                      </td>
                      <td>
                        <input matInput formControlName="unitPrice" type="number" step="0.01" class="inline-input" (input)="calculateLineTotal($index)">
                      </td>
                      <td>{{ items.at($index).get('lineTotal')?.value | currency }}</td>
                      <td>
                        <button mat-icon-button color="warn" type="button" (click)="removeItem($index)">
                          <mat-icon>delete</mat-icon>
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>

            <div class="total-row">
              <strong>Grand Total: {{ grandTotal | currency }}</strong>
            </div>

            <div class="form-actions">
              <button mat-button type="button" (click)="cancel()">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving || items.length === 0">
                @if (saving) { <mat-spinner diameter="20"></mat-spinner> } @else { Create Order }
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .form-container { max-width: 900px; margin: 0 auto; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 0 16px; }
    .form-grid mat-form-field { width: 100%; }
    h3 { display: flex; align-items: center; gap: 12px; margin: 24px 0 16px; }
    .full-width { width: 100%; }
    .inline-input { width: 100px; padding: 8px; border: 1px solid #ccc; border-radius: 4px; }
    .inline-select { width: 200px; }
    .items-table { border-collapse: collapse; }
    .items-table th, .items-table td { padding: 8px 12px; text-align: left; border-bottom: 1px solid #e0e0e0; }
    .items-table th { font-weight: 500; color: rgba(0,0,0,.54); }
    .total-row { text-align: right; padding: 16px 0; font-size: 1.1rem; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 16px; }
  `],
})
export class PoFormComponent implements OnInit {
  form: FormGroup;
  suppliers: SupplierDto[] = [];
  warehouses: WarehouseDto[] = [];
  products: ProductDto[] = [];
  saving = false;
  itemColumns = ['product', 'quantity', 'unitPrice', 'lineTotal', 'remove'];

  constructor(
    private fb: FormBuilder, private poService: PurchaseOrderService,
    private supplierService: SupplierService, private warehouseService: WarehouseService,
    private productService: ProductService, private router: Router, private notification: NotificationService
  ) {
    this.form = this.fb.group({
      supplierId: ['', [Validators.required]],
      warehouseId: ['', [Validators.required]],
      expectedDeliveryDate: [null],
      items: this.fb.array([]),
    });
  }

  get items(): FormArray { return this.form.get('items') as FormArray; }

  get grandTotal(): number {
    return this.items.controls.reduce((sum, item) => sum + (item.get('lineTotal')?.value || 0), 0);
  }

  ngOnInit(): void {
    this.supplierService.getAll({ pageSize: 200 }).subscribe({ next: (r) => this.suppliers = r.items });
    this.warehouseService.getAll({ pageSize: 100 }).subscribe({ next: (r) => this.warehouses = r.items });
    this.productService.getAll({ pageSize: 200 }).subscribe({ next: (r) => this.products = r.items });
    this.addItem();
  }

  addItem(): void {
    this.items.push(this.fb.group({
      productId: ['', [Validators.required]],
      quantity: [1, [Validators.required, Validators.min(1)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      lineTotal: [{ value: 0, disabled: true }],
    }));
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  calculateLineTotal(index: number): void {
    const item = this.items.at(index);
    const qty = item.get('quantity')?.value || 0;
    const price = item.get('unitPrice')?.value || 0;
    item.get('lineTotal')?.setValue(qty * price);
  }

  onSubmit(): void {
    if (this.form.invalid || this.items.length === 0) return;
    this.saving = true;

    const data = {
      ...this.form.value,
      items: this.items.getRawValue(),
      expectedDeliveryDate: this.form.value.expectedDeliveryDate
        ? new Date(this.form.value.expectedDeliveryDate).toISOString() : null,
    };

    this.poService.create(data).subscribe({
      next: () => {
        this.notification.success('Purchase order created');
        this.router.navigate(['/purchase-orders']);
      },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/purchase-orders']); }
}
