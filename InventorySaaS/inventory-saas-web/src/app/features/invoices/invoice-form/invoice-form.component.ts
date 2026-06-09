import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { InvoiceService } from '../../../core/services/invoice.service';
import { CustomerService } from '../../../core/services/customer.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CustomerDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-invoice-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, MatIconModule],
  templateUrl: './invoice-form.component.html',
  styleUrl: './invoice-form.component.css',
})
export class InvoiceFormComponent implements OnInit {
  form!: FormGroup;
  customers: CustomerDto[] = [];
  saving = false;

  constructor(
    private fb: FormBuilder, private invoiceService: InvoiceService,
    private customerService: CustomerService, private notification: NotificationService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.form = this.fb.group({
      customerId: ['', Validators.required],
      dueDate: [''],
      notes: [''],
      items: this.fb.array([this.newItem()]),
    });

    this.customerService.getAll({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (r) => { this.customers = r.items; },
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
      customerId: v.customerId,
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
    this.invoiceService.create(payload).subscribe({
      next: (inv) => { this.notification.success('Invoice created'); this.router.navigate(['/invoices', inv.id]); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/invoices']); }
}
