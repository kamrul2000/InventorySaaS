import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP, MANAGER_UP } from '../../../core/constants/roles';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { SupplierBillDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-bill-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './supplier-bill-detail.component.html',
  styleUrl: './supplier-bill-detail.component.css',
})
export class SupplierBillDetailComponent implements OnInit {
  bill: SupplierBillDto | null = null;
  loading = true;

  constructor(
    private billService: SupplierBillService, private route: ActivatedRoute,
    private router: Router, private notification: NotificationService,
    private authService: AuthService, private dialog: MatDialog
  ) {}

  /** Mirrors the API's StaffUp policy on POST /SupplierBills/{id}/approve. */
  get canApprove(): boolean {
    if (!this.bill || this.bill.status !== 'Draft') return false;
    return this.authService.hasAnyRole(STAFF_UP);
  }

  /** Mirrors the API's ManagerUp policy on POST /SupplierBills/{id}/cancel. */
  get canCancel(): boolean {
    if (!this.bill || this.bill.status === 'Cancelled' || this.bill.amountPaid !== 0) return false;
    return this.authService.hasAnyRole(MANAGER_UP);
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.billService.getById(id).subscribe({
        next: (bill) => { this.bill = bill; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  /** Mirrors the API's StaffUp policy on POST /SupplierPayments (recordPayment creates one). */
  get canPay(): boolean {
    if (!this.bill) return false;
    if (this.bill.status === 'Draft' || this.bill.status === 'Cancelled' || this.bill.balanceDue <= 0) return false;
    return this.authService.hasAnyRole(STAFF_UP);
  }

  approve(): void {
    if (!this.bill) return;
    this.billService.approve(this.bill.id).subscribe({
      next: () => { this.notification.success('Bill approved'); this.ngOnInit(); },
    });
  }

  cancel(): void {
    if (!this.bill) return;
    const billId = this.bill.id;

    const dialogRef = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      panelClass: 'confirm-dialog-panel',
      data: { title: 'Cancel Bill', message: 'Cancel this bill?' },
    });

    dialogRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) return;
      this.billService.cancel(billId).subscribe({
        next: () => { this.notification.success('Bill cancelled'); this.ngOnInit(); },
      });
    });
  }

  recordPayment(): void {
    if (!this.bill) return;
    this.router.navigate(['/supplier-payments/new'], {
      queryParams: { supplierId: this.bill.supplierId, billId: this.bill.id },
    });
  }

  back(): void { this.router.navigate(['/supplier-bills']); }
}
