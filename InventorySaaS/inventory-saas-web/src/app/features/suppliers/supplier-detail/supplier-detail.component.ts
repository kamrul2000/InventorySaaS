import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SupplierService } from '../../../core/services/supplier.service';
import { SupplierDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-supplier-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './supplier-detail.component.html',
  styleUrl: './supplier-detail.component.css',
})
export class SupplierDetailComponent implements OnInit {
  supplier: SupplierDto | null = null;
  loading = true;

  constructor(
    private supplierService: SupplierService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.supplierService.getById(id).subscribe({
        next: (s) => { this.supplier = s; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.supplier) this.router.navigate(['/suppliers', this.supplier.id, 'edit']); }
  back(): void { this.router.navigate(['/suppliers']); }
}
