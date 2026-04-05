import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ProductService } from '../../../core/services/product.service';
import { ProductDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-product-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './product-detail.component.html',
  styleUrl: './product-detail.component.css',
})
export class ProductDetailComponent implements OnInit {
  product: ProductDto | null = null;
  loading = true;

  constructor(
    private productService: ProductService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.productService.getById(id).subscribe({
        next: (p) => { this.product = p; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.product) this.router.navigate(['/products', this.product.id, 'edit']); }
  back(): void { this.router.navigate(['/products']); }
}
