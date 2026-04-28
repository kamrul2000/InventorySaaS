import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ProductService } from '../../../core/services/product.service';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CategoryDto, ProductExtractionResult } from '../../../core/models/domain.models';

const MAX_IMAGE_BYTES = 5 * 1024 * 1024;
const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png'];

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './product-form.component.html',
  styleUrl: './product-form.component.css',
})
export class ProductFormComponent implements OnInit {
  @ViewChild('fileInput') fileInput?: ElementRef<HTMLInputElement>;

  form: FormGroup;
  isEditMode = false;
  saving = false;
  scanning = false;
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

  openImagePicker(): void {
    if (this.scanning) return;

    if (this.form.dirty) {
      const ok = window.confirm(
        'Scanning a photo will overwrite values you have already entered. Continue?'
      );
      if (!ok) return;
    }

    this.fileInput?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      this.notification.error('Only JPEG and PNG images are supported.');
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      this.notification.error('Image must be 5 MB or smaller.');
      return;
    }

    this.scanning = true;
    this.productService.extractFromImage(file).subscribe({
      next: (result) => {
        this.applyExtraction(result);
        this.scanning = false;
      },
      error: () => {
        this.scanning = false;
      },
    });
  }

  private applyExtraction(result: ProductExtractionResult): void {
    const patch: Record<string, unknown> = {};

    if (result.name) patch['name'] = result.name;
    if (result.barcode) patch['barcode'] = result.barcode;
    if (result.brandName) patch['brandName'] = result.brandName;
    if (result.unitName) patch['unitName'] = result.unitName;
    if (result.suggestedSellingPrice != null) patch['sellingPrice'] = result.suggestedSellingPrice;
    if (result.suggestedCostPrice != null) patch['costPrice'] = result.suggestedCostPrice;
    patch['trackExpiry'] = result.trackExpiry;

    let categoryMatched = true;
    if (result.suggestedCategory) {
      const matchedId = this.findCategoryId(result.suggestedCategory);
      if (matchedId) {
        patch['categoryId'] = matchedId;
      } else {
        categoryMatched = false;
      }
    }

    this.form.patchValue(patch);
    this.form.markAsDirty();

    const summary = [`Extracted "${result.name ?? 'product'}".`];
    if (!categoryMatched) {
      summary.push(`No exact match for category "${result.suggestedCategory}" — please pick one.`);
    }
    if (result.notes) {
      summary.push(result.notes);
    }
    this.notification.success(summary.join(' '));
  }

  private findCategoryId(suggested: string): string | null {
    const normalized = suggested.trim().toLowerCase();
    const exact = this.categories.find((c) => c.name.toLowerCase() === normalized);
    if (exact) return exact.id;
    const partial = this.categories.find(
      (c) => c.name.toLowerCase().includes(normalized) || normalized.includes(c.name.toLowerCase())
    );
    return partial?.id ?? null;
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
