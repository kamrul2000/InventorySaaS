import { Component, ElementRef, OnInit, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDialog } from '@angular/material/dialog';
import { ProductService } from '../../../core/services/product.service';
import { CategoryService } from '../../../core/services/category.service';
import { BrandService } from '../../../core/services/brand.service';
import { UnitOfMeasureService } from '../../../core/services/unit-of-measure.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BrandFormComponent } from '../../brands/brand-form/brand-form.component';
import { UnitFormComponent } from '../../units/unit-form/unit-form.component';
import { BrandDto, CategoryDto, ProductExtractionResult, UnitOfMeasureDto } from '../../../core/models/domain.models';

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
  brands: BrandDto[] = [];
  units: UnitOfMeasureDto[] = [];

  constructor(
    private fb: FormBuilder,
    private productService: ProductService,
    private categoryService: CategoryService,
    private brandService: BrandService,
    private unitService: UnitOfMeasureService,
    private dialog: MatDialog,
    private router: Router,
    private route: ActivatedRoute,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      sku: [''],
      barcode: [''],
      categoryId: ['', [Validators.required]],
      brandId: [null],
      unitOfMeasureId: ['', [Validators.required]],
      costPrice: [0, [Validators.required, Validators.min(0)]],
      sellingPrice: [0, [Validators.required, Validators.min(0)]],
      reorderLevel: [0],
      trackExpiry: [false],
      // Drive how strictly stock movements and scanning handle this product.
      trackBatch: [false],
      trackSerial: [false],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    this.loadCategories();
    this.loadBrands();
    this.loadUnits();

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

  loadBrands(): void {
    this.brandService.getAll({ pageSize: 200 }).subscribe({
      next: (result) => {
        this.brands = result.items.filter((b) => b.isActive);
      },
    });
  }

  loadUnits(): void {
    this.unitService.getAll({ pageSize: 200 }).subscribe({
      next: (result) => {
        this.units = result.items.filter((u) => u.isActive);
      },
    });
  }

  /** Adds a brand without leaving the product form, then selects it. */
  addBrand(): void {
    const dialogRef = this.dialog.open(BrandFormComponent, { width: '500px', data: {} });
    dialogRef.afterClosed().subscribe((brand?: BrandDto) => {
      if (!brand) return;
      this.brands = [...this.brands, brand].sort((a, b) => a.name.localeCompare(b.name));
      this.form.patchValue({ brandId: brand.id });
      this.form.markAsDirty();
    });
  }

  /** Adds a unit without leaving the product form, then selects it. */
  addUnit(): void {
    const dialogRef = this.dialog.open(UnitFormComponent, { width: '500px', data: {} });
    dialogRef.afterClosed().subscribe((unit?: UnitOfMeasureDto) => {
      if (!unit) return;
      this.units = [...this.units, unit].sort((a, b) => a.name.localeCompare(b.name));
      this.form.patchValue({ unitOfMeasureId: unit.id });
      this.form.markAsDirty();
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
    if (result.suggestedSellingPrice != null) patch['sellingPrice'] = result.suggestedSellingPrice;
    if (result.suggestedCostPrice != null) patch['costPrice'] = result.suggestedCostPrice;
    patch['trackExpiry'] = result.trackExpiry;

    // The model returns names; the form holds ids. Anything that doesn't match an existing
    // record is reported rather than auto-created, so the master lists stay curated.
    const unmatched: string[] = [];

    if (result.suggestedCategory) {
      const categoryId = this.findIdByName(this.categories, result.suggestedCategory);
      if (categoryId) patch['categoryId'] = categoryId;
      else unmatched.push(`category "${result.suggestedCategory}"`);
    }

    if (result.brandName) {
      const brandId = this.findIdByName(this.brands, result.brandName);
      if (brandId) patch['brandId'] = brandId;
      else unmatched.push(`brand "${result.brandName}"`);
    }

    if (result.unitName) {
      const unitId = this.findIdByName(this.units, result.unitName);
      if (unitId) patch['unitOfMeasureId'] = unitId;
      else unmatched.push(`unit "${result.unitName}"`);
    }

    this.form.patchValue(patch);
    this.form.markAsDirty();

    const summary = [`Extracted "${result.name ?? 'product'}".`];
    if (unmatched.length > 0) {
      summary.push(`No match for ${unmatched.join(', ')} — pick one or add it.`);
    }
    if (result.notes) {
      summary.push(result.notes);
    }
    this.notification.success(summary.join(' '));
  }

  private findIdByName(options: { id: string; name: string }[], suggested: string): string | null {
    const normalized = suggested.trim().toLowerCase();
    const exact = options.find((o) => o.name.toLowerCase() === normalized);
    if (exact) return exact.id;
    const partial = options.find(
      (o) => o.name.toLowerCase().includes(normalized) || normalized.includes(o.name.toLowerCase())
    );
    return partial?.id ?? null;
  }

  onSubmit(): void {
    if (this.form.invalid) return;

    this.saving = true;
    const data = this.form.value;

    const request = this.isEditMode
      // A null brandId reads as "unchanged" on update, so removing one has to be said explicitly.
      ? this.productService.update(this.productId!, { ...data, clearBrand: !data.brandId })
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
