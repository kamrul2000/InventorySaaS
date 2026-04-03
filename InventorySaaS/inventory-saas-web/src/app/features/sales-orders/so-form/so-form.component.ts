import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { CustomerService } from '../../../core/services/customer.service';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { ProductService } from '../../../core/services/product.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CustomerDto, WarehouseDto, ProductDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-so-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule],
  templateUrl: './so-form.component.html',
  styleUrl: './so-form.component.css',
})
export class SoFormComponent implements OnInit {
  form: FormGroup;
  customers: CustomerDto[] = [];
  warehouses: WarehouseDto[] = [];
  products: ProductDto[] = [];
  saving = false;

  constructor(
    private fb: FormBuilder, private soService: SalesOrderService,
    private customerService: CustomerService, private warehouseService: WarehouseService,
    private productService: ProductService, private router: Router, private notification: NotificationService
  ) {
    this.form = this.fb.group({
      customerId: ['', [Validators.required]],
      warehouseId: ['', [Validators.required]],
      deliveryDate: [null],
      shippingAddress: [''],
      items: this.fb.array([]),
    });
  }

  get items(): FormArray { return this.form.get('items') as FormArray; }
  get grandTotal(): number {
    return this.items.controls.reduce((sum, item) => sum + (item.get('lineTotal')?.value || 0), 0);
  }

  ngOnInit(): void {
    this.customerService.getAll({ pageSize: 200 }).subscribe({ next: (r) => this.customers = r.items });
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

  removeItem(index: number): void { this.items.removeAt(index); }

  calculateLineTotal(index: number): void {
    const item = this.items.at(index);
    item.get('lineTotal')?.setValue((item.get('quantity')?.value || 0) * (item.get('unitPrice')?.value || 0));
  }

  onSubmit(): void {
    if (this.form.invalid || this.items.length === 0) return;
    this.saving = true;
    const data = {
      ...this.form.value,
      items: this.items.getRawValue(),
      deliveryDate: this.form.value.deliveryDate ? new Date(this.form.value.deliveryDate).toISOString() : null,
    };
    this.soService.create(data).subscribe({
      next: () => { this.notification.success('Sales order created'); this.router.navigate(['/sales-orders']); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/sales-orders']); }
}
