import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SupplierPaymentService } from '../../../core/services/supplier-payment.service';
import { SupplierPaymentDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-payment-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './supplier-payment-detail.component.html',
  styleUrl: './supplier-payment-detail.component.css',
})
export class SupplierPaymentDetailComponent implements OnInit {
  payment: SupplierPaymentDto | null = null;
  loading = true;

  constructor(
    private paymentService: SupplierPaymentService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.paymentService.getById(id).subscribe({
        next: (p) => { this.payment = p; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  back(): void { this.router.navigate(['/supplier-payments']); }
}
