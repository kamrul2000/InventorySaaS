import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SupplierDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-bill-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule],
  templateUrl: './supplier-bill-form.component.html',
  styleUrl: './supplier-bill-form.component.css',
})
export class SupplierBillFormComponent implements OnInit {
  form!: FormGroup;
  suppliers: SupplierDto[] = [];
  saving = false;

  constructor(
    private fb: FormBuilder, private billService: SupplierBillService,
    private supplierService: SupplierService, private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      supplierId: ['', Validators.required],
      supplierInvoiceNumber: [''],
      dueDate: [''],
      notes: [''],
      items: this.fb.array([this.newItem()]),
    });

    this.supplierService.getAll({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (r) => { this.suppliers = r.items; },
    });
  }

  get items(): FormArray { return this.form.get('items') as FormArray; }

  newItem(): FormGroup {
    return this.fb.group({
      description: ['', Validators.required],
      quantity: [1, [Validators.required, Validators.min(1)]],
      unitPrice: [0, [Validators.required, Validators.min(0)]],
      taxRate: [0, [Validators.min(0)]],
      discountRate: [0, [Validators.min(0)]],
    });
  }

  addItem(): void { this.items.push(this.newItem()); }
  removeItem(i: number): void { if (this.items.length > 1) this.items.removeAt(i); }

  lineTotal(group: any): number {
    const sub = (group.value.quantity || 0) * (group.value.unitPrice || 0);
    return sub + sub * ((group.value.taxRate || 0) / 100) - sub * ((group.value.discountRate || 0) / 100);
  }

  get grandTotal(): number {
    return this.items.controls.reduce((sum, g) => sum + this.lineTotal(g), 0);
  }

  submit(): void {
    if (this.form.invalid) { this.form.markAllAsTouched(); return; }
    this.saving = true;
    const v = this.form.value;
    const payload = {
      supplierId: v.supplierId,
      supplierInvoiceNumber: v.supplierInvoiceNumber || null,
      dueDate: v.dueDate || null,
      notes: v.notes || null,
      items: v.items.map((it: any) => ({
        productId: null,
        description: it.description,
        quantity: it.quantity,
        unitPrice: it.unitPrice,
        taxRate: it.taxRate,
        discountRate: it.discountRate,
      })),
    };
    this.billService.create(payload).subscribe({
      next: (bill) => { this.notification.success('Bill created'); this.router.navigate(['/supplier-bills', bill.id]); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/supplier-bills']); }
}
