import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
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
    CommonModule, ReactiveFormsModule, MatIconModule, MatProgressSpinnerModule,
  ],
  templateUrl: './po-form.component.html',
  styleUrl: './po-form.component.css',
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
