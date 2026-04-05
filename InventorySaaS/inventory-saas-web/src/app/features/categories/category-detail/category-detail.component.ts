import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CategoryService } from '../../../core/services/category.service';
import { CategoryDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-category-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './category-detail.component.html',
  styleUrl: './category-detail.component.css',
})
export class CategoryDetailComponent implements OnInit {
  category: CategoryDto | null = null;
  loading = true;

  constructor(
    private categoryService: CategoryService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.categoryService.getById(id).subscribe({
        next: (c) => { this.category = c; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  back(): void { this.router.navigate(['/categories']); }
}
