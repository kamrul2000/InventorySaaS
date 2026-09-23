import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PaymentService } from '../../../core/services/payment.service';
import { PaymentDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-payment-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './payment-detail.component.html',
  styleUrl: './payment-detail.component.css',
})
export class PaymentDetailComponent implements OnInit {
  payment: PaymentDto | null = null;
  loading = true;

  constructor(
    private paymentService: PaymentService,
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

  back(): void { this.router.navigate(['/payments']); }
}
