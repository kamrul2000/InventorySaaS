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
  template: `
    <div class="form-container">
      <mat-card>
        <mat-card-header>
          <mat-card-title>{{ isEditMode ? 'Edit Product' : 'New Product' }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Product Name</mat-label>
                <input matInput formControlName="name" placeholder="Enter product name">
                @if (form.get('name')?.hasError('required') && form.get('name')?.touched) {
                  <mat-error>Name is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>SKU</mat-label>
                <input matInput formControlName="sku" placeholder="Auto-generated if empty">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Barcode</mat-label>
                <input matInput formControlName="barcode" placeholder="Barcode">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Category</mat-label>
                <mat-select formControlName="categoryId">
                  @for (cat of categories; track cat.id) {
                    <mat-option [value]="cat.id">{{ cat.name }}</mat-option>
                  }
                </mat-select>
                @if (form.get('categoryId')?.hasError('required') && form.get('categoryId')?.touched) {
                  <mat-error>Category is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Brand</mat-label>
                <input matInput formControlName="brandName" placeholder="Brand name">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Unit of Measure</mat-label>
                <input matInput formControlName="unitName" placeholder="e.g., Piece, Kg">
                @if (form.get('unitName')?.hasError('required') && form.get('unitName')?.touched) {
                  <mat-error>Unit is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Cost Price</mat-label>
                <input matInput formControlName="costPrice" type="number" step="0.01">
                @if (form.get('costPrice')?.hasError('required') && form.get('costPrice')?.touched) {
                  <mat-error>Cost price is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Selling Price</mat-label>
                <input matInput formControlName="sellingPrice" type="number" step="0.01">
                @if (form.get('sellingPrice')?.hasError('required') && form.get('sellingPrice')?.touched) {
                  <mat-error>Selling price is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Reorder Level</mat-label>
                <input matInput formControlName="reorderLevel" type="number">
              </mat-form-field>
            </div>

            <div class="checkbox-row">
              <mat-checkbox formControlName="trackExpiry">Track Expiry</mat-checkbox>
              <mat-checkbox formControlName="isActive">Active</mat-checkbox>
            </div>

            <div class="form-actions">
              <button mat-button type="button" (click)="cancel()">Cancel</button>
              <button mat-flat-button color="primary" type="submit"
                      [disabled]="form.invalid || saving">
                @if (saving) {
                  <mat-spinner diameter="20"></mat-spinner>
                } @else {
                  {{ isEditMode ? 'Update' : 'Create' }}
                }
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .form-container {
      max-width: 800px;
      margin: 0 auto;
    }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 0 16px;
    }
    .form-grid mat-form-field {
      width: 100%;
    }
    .checkbox-row {
      display: flex;
      gap: 24px;
      margin: 16px 0;
    }
    .form-actions {
      display: flex;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 24px;
    }
  `],
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
