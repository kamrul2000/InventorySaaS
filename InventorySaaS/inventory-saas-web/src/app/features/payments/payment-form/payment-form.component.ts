import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { InvoiceService } from '../../../core/services/invoice.service';
import { PaymentService } from '../../../core/services/payment.service';
import { CustomerService } from '../../../core/services/customer.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CustomerDto, OutstandingInvoiceDto } from '../../../core/models/domain.models';

interface AllocationRow {
  invoice: OutstandingInvoiceDto;
  allocate: number;
}

@Component({
  selector: 'app-payment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './payment-form.component.html',
  styleUrl: './payment-form.component.css',
})
export class PaymentFormComponent implements OnInit {
  customers: CustomerDto[] = [];
  rows: AllocationRow[] = [];
  saving = false;

  customerId = '';
  amount = 0;
  method = 'Cash';
  reference = '';
  notes = '';
  paymentDate = '';

  readonly methods = ['Cash', 'BankTransfer', 'Card', 'Cheque', 'MobileBanking', 'Other'];

  private preselectInvoiceId: string | null = null;

  constructor(
    private invoiceService: InvoiceService, private paymentService: PaymentService,
    private customerService: CustomerService, private notification: NotificationService,
    private route: ActivatedRoute, private router: Router
  ) {}

  ngOnInit(): void {
    this.customerId = this.route.snapshot.queryParamMap.get('customerId') || '';
    this.preselectInvoiceId = this.route.snapshot.queryParamMap.get('invoiceId');

    this.customerService.getAll({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (r) => { this.customers = r.items; },
    });

    if (this.customerId) this.loadOutstanding();
  }

  onCustomerChange(): void {
    this.preselectInvoiceId = null;
    this.rows = [];
    this.amount = 0;
    if (this.customerId) this.loadOutstanding();
  }

  loadOutstanding(): void {
    this.invoiceService.getOutstanding(this.customerId).subscribe({
      next: (invoices) => {
        this.rows = invoices.map((inv) => ({
          invoice: inv,
          allocate: inv.id === this.preselectInvoiceId ? inv.balanceDue : 0,
        }));
        if (this.preselectInvoiceId) {
          const match = this.rows.find((r) => r.invoice.id === this.preselectInvoiceId);
          if (match) this.amount = match.allocate;
        }
      },
    });
  }

  /** Distribute the entered payment amount across outstanding invoices, oldest first. */
  autoAllocate(): void {
    let remaining = this.amount;
    for (const row of this.rows) {
      const take = Math.min(row.invoice.balanceDue, Math.max(0, remaining));
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
    if (!this.customerId) { this.notification.error('Select a customer'); return; }
    if (this.amount <= 0) { this.notification.error('Enter a payment amount'); return; }
    if (this.totalAllocated > this.amount) {
      this.notification.error('Allocated total exceeds the payment amount');
      return;
    }
    const allocations = this.rows
      .filter((r) => Number(r.allocate) > 0)
      .map((r) => ({ invoiceId: r.invoice.id, amount: Number(r.allocate) }));

    this.saving = true;
    this.paymentService.create({
      customerId: this.customerId,
      paymentDate: this.paymentDate || null,
      amount: this.amount,
      method: this.method,
      reference: this.reference || null,
      notes: this.notes || null,
      allocations,
    }).subscribe({
      next: () => { this.notification.success('Payment recorded'); this.router.navigate(['/payments']); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/payments']); }
}
