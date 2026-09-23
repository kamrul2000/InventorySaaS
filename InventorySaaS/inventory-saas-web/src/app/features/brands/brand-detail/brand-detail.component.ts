import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { BrandService } from '../../../core/services/brand.service';
import { BrandDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-brand-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './brand-detail.component.html',
  styleUrl: './brand-detail.component.css',
})
export class BrandDetailComponent implements OnInit {
  brand: BrandDto | null = null;
  loading = true;

  constructor(
    private brandService: BrandService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.brandService.getById(id).subscribe({
        next: (b) => { this.brand = b; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.brand) this.router.navigate(['/brands', this.brand.id, 'edit']); }
  back(): void { this.router.navigate(['/brands']); }
}
