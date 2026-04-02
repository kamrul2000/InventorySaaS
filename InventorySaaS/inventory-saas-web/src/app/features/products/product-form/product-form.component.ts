import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ProductService } from '../../../core/services/product.service';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CategoryDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.css',
})
export class ProductFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  productId: string | null = null;
  categories: CategoryDto[] = [];

  constructor(
    private fb: FormBuilder,
    private productService: ProductService,
    private categoryService: CategoryService,
    private router: Router,
    private route: ActivatedRoute,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      sku: [''],
      barcode: [''],
      categoryId: ['', [Validators.required]],
      brandName: [''],
      unitName: ['', [Validators.required]],
      costPrice: [0, [Validators.required, Validators.min(0)]],
      sellingPrice: [0, [Validators.required, Validators.min(0)]],
      reorderLevel: [0],
      trackExpiry: [false],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    this.loadCategories();

    this.productId = this.route.snapshot.paramMap.get('id');
    if (this.productId) {
      this.isEditMode = true;
      this.productService.getById(this.productId).subscribe({
        next: (product) => {
          this.form.patchValue(product);
        },
      });
    }
  }

  loadCategories(): void {
    this.categoryService.getAll({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.categories = result.items;
      },
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.saving = true;
    const data = this.form.value;

    const request = this.isEditMode
      ? this.productService.update(this.productId!, data)
      : this.productService.create(data);

    request.subscribe({
      next: () => {
        this.notification.success(this.isEditMode ? 'Product updated' : 'Product created');
        this.router.navigate(['/products']);
      },
      error: () => {
        this.saving = false;
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/products']);
  }
}
