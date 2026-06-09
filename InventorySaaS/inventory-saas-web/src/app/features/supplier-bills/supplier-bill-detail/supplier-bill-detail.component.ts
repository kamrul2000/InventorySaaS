import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { NotificationService } from '../../../core/services/notification.service';
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
    private router: Router, private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.billService.getById(id).subscribe({
        next: (bill) => { this.bill = bill; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  get canPay(): boolean {
    if (!this.bill) return false;
    return this.bill.status !== 'Draft'
      && this.bill.status !== 'Cancelled'
      && this.bill.balanceDue > 0;
  }

  approve(): void {
    if (!this.bill) return;
    this.billService.approve(this.bill.id).subscribe({
      next: () => { this.notification.success('Bill approved'); this.ngOnInit(); },
    });
  }

  cancel(): void {
    if (!this.bill) return;
    if (!confirm('Cancel this bill?')) return;
    this.billService.cancel(this.bill.id).subscribe({
      next: () => { this.notification.success('Bill cancelled'); this.ngOnInit(); },
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
