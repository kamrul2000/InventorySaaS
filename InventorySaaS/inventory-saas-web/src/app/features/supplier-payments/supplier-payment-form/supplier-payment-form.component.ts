import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { SupplierPaymentService } from '../../../core/services/supplier-payment.service';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SupplierDto, OutstandingBillDto } from '../../../core/models/domain.models';

interface AllocationRow {
  bill: OutstandingBillDto;
  allocate: number;
}

@Component({
  selector: 'app-supplier-payment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './supplier-payment-form.component.html',
  styleUrl: './supplier-payment-form.component.css',
})
export class SupplierPaymentFormComponent implements OnInit {
  suppliers: SupplierDto[] = [];
  rows: AllocationRow[] = [];
  saving = false;

  supplierId = '';
  amount = 0;
  method = 'Cash';
  reference = '';
  notes = '';
  paymentDate = '';

  readonly methods = ['Cash', 'BankTransfer', 'Card', 'Cheque', 'MobileBanking', 'Other'];

  private preselectBillId: string | null = null;

  constructor(
    private billService: SupplierBillService, private paymentService: SupplierPaymentService,
    private supplierService: SupplierService, private notification: NotificationService,
    private route: ActivatedRoute, private router: Router
  ) {}

  ngOnInit(): void {
    this.supplierId = this.route.snapshot.queryParamMap.get('supplierId') || '';
    this.preselectBillId = this.route.snapshot.queryParamMap.get('billId');

    this.supplierService.getAll({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (r) => { this.suppliers = r.items; },
    });

    if (this.supplierId) this.loadOutstanding();
  }

  onSupplierChange(): void {
    this.preselectBillId = null;
    this.rows = [];
    this.amount = 0;
    if (this.supplierId) this.loadOutstanding();
  }

  loadOutstanding(): void {
    this.billService.getOutstanding(this.supplierId).subscribe({
      next: (bills) => {
        this.rows = bills.map((bill) => ({
          bill,
          allocate: bill.id === this.preselectBillId ? bill.balanceDue : 0,
        }));
        if (this.preselectBillId) {
          const match = this.rows.find((r) => r.bill.id === this.preselectBillId);
          if (match) this.amount = match.allocate;
        }
      },
    });
  }

  /** Distribute the entered payment amount across outstanding bills, oldest first. */
  autoAllocate(): void {
    let remaining = this.amount;
    for (const row of this.rows) {
      const take = Math.min(row.bill.balanceDue, Math.max(0, remaining));
      row.allocate = +take.toFixed(2);
      remaining = +(remaining - take).toFixed(2);
    }
  }

  get totalAllocated(): number {
    return +this.rows.reduce((sum, r) => sum + (Number(r.allocate) || 0), 0).toFixed(2);
  }

  get unallocated(): number {
    return +(this.amount - this.totalAllocated).toFixed(2);
  }

  submit(): void {
    if (!this.supplierId) { this.notification.error('Select a supplier'); return; }
    if (this.amount <= 0) { this.notification.error('Enter a payment amount'); return; }
    if (this.totalAllocated > this.amount) {
      this.notification.error('Allocated total exceeds the payment amount');
      return;
    }
    const allocations = this.rows
      .filter((r) => Number(r.allocate) > 0)
      .map((r) => ({ billId: r.bill.id, amount: Number(r.allocate) }));

    this.saving = true;
    this.paymentService.create({
      supplierId: this.supplierId,
      paymentDate: this.paymentDate || null,
      amount: this.amount,
      method: this.method,
      reference: this.reference || null,
      notes: this.notes || null,
      allocations,
    }).subscribe({
      next: () => { this.notification.success('Payment recorded'); this.router.navigate(['/supplier-payments']); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/supplier-payments']); }
}
