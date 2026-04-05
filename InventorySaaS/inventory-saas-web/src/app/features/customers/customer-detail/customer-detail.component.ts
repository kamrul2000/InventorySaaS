import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CustomerService } from '../../../core/services/customer.service';
import { CustomerDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-customer-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './customer-detail.component.html',
  styleUrl: './customer-detail.component.css',
})
export class CustomerDetailComponent implements OnInit {
  customer: CustomerDto | null = null;
  loading = true;

  constructor(
    private customerService: CustomerService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.customerService.getById(id).subscribe({
        next: (c) => { this.customer = c; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.customer) this.router.navigate(['/customers', this.customer.id, 'edit']); }
  back(): void { this.router.navigate(['/customers']); }
}
