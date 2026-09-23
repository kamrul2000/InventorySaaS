import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { InvoiceService } from '../../../core/services/invoice.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP, MANAGER_UP } from '../../../core/constants/roles';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
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
    private router: Router, private notification: NotificationService,
    private authService: AuthService, private dialog: MatDialog
  ) {}

  /** Mirrors the API's StaffUp policy on POST /Invoices/{id}/issue. */
  get canIssue(): boolean {
    if (!this.invoice || this.invoice.status !== 'Draft') return false;
    return this.authService.hasAnyRole(STAFF_UP);
  }

  /** Mirrors the API's ManagerUp policy on POST /Invoices/{id}/cancel. */
  get canCancel(): boolean {
    if (!this.invoice || this.invoice.status === 'Cancelled' || this.invoice.amountPaid !== 0) return false;
    return this.authService.hasAnyRole(MANAGER_UP);
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.invoiceService.getById(id).subscribe({
        next: (invoice) => { this.invoice = invoice; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  /** Mirrors the API's StaffUp policy on POST /Payments (recordPayment creates one). */
  get canPay(): boolean {
    if (!this.invoice) return false;
    if (this.invoice.status === 'Draft' || this.invoice.status === 'Cancelled' || this.invoice.balanceDue <= 0) return false;
    return this.authService.hasAnyRole(STAFF_UP);
  }

  issue(): void {
    if (!this.invoice) return;
    this.invoiceService.issue(this.invoice.id).subscribe({
      next: () => { this.notification.success('Invoice issued'); this.ngOnInit(); },
    });
  }

  cancel(): void {
    if (!this.invoice) return;
    const invoiceId = this.invoice.id;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      panelClass: 'confirm-dialog-panel',
      data: { title: 'Cancel Invoice', message: 'Cancel this invoice?' },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) return;
      this.invoiceService.cancel(invoiceId).subscribe({
        next: () => { this.notification.success('Invoice cancelled'); this.ngOnInit(); },
      });
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
