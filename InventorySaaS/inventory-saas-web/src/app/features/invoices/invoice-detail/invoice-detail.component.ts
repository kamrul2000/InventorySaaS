import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { InvoiceService } from '../../../core/services/invoice.service';
import { NotificationService } from '../../../core/services/notification.service';
import { InvoiceDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-invoice-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './invoice-detail.component.html',
  styleUrl: './invoice-detail.component.css',
})
export class InvoiceDetailComponent implements OnInit {
  invoice: InvoiceDto | null = null;
  loading = true;

  constructor(
    private invoiceService: InvoiceService, private route: ActivatedRoute,
    private router: Router, private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.invoiceService.getById(id).subscribe({
        next: (invoice) => { this.invoice = invoice; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  get canPay(): boolean {
    if (!this.invoice) return false;
    return this.invoice.status !== 'Draft'
      && this.invoice.status !== 'Cancelled'
      && this.invoice.balanceDue > 0;
  }

  issue(): void {
    if (!this.invoice) return;
    this.invoiceService.issue(this.invoice.id).subscribe({
      next: () => { this.notification.success('Invoice issued'); this.ngOnInit(); },
    });
  }

  cancel(): void {
    if (!this.invoice) return;
    if (!confirm('Cancel this invoice?')) return;
    this.invoiceService.cancel(this.invoice.id).subscribe({
      next: () => { this.notification.success('Invoice cancelled'); this.ngOnInit(); },
    });
  }

  recordPayment(): void {
    if (!this.invoice) return;
    this.router.navigate(['/payments/new'], {
      queryParams: { customerId: this.invoice.customerId, invoiceId: this.invoice.id },
    });
  }

  back(): void { this.router.navigate(['/invoices']); }
}
