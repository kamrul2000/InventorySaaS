import { CommonModule } from '@angular/common';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { SearchableSelectModule } from '../../../shared/searchable-select/searchable-select.module';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CategoryDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-category-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterLink,
    MatIconModule,
    MatFormFieldModule,
    SearchableSelectModule,
  ],
  templateUrl: './category-form.component.html',
  styleUrl: './category-form.component.css',
})
export class CategoryFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  categoryId: string | null = null;
  parentCategories: CategoryDto[] = [];

  constructor(
    private fb: FormBuilder,
    private categoryService: CategoryService,
    private notification: NotificationService,
    private router: Router,
    private route: ActivatedRoute,
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      parentCategoryId: [null],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    this.categoryId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.categoryId;
    this.searchParentCategories('');

    if (this.categoryId) {
      this.categoryService.getById(this.categoryId).subscribe({
        next: (category) => this.form.patchValue(category),
      });
    }
  }

  searchParentCategories(search: string): void {
    this.categoryService.getAll({ pageSize: 100, search }).subscribe({
      next: (result) => {
        this.parentCategories = result.items.filter((category) => category.id !== this.categoryId);
      },
    });
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    const request = this.isEditMode
      ? this.categoryService.update(this.categoryId!, this.form.value)
      : this.categoryService.create(this.form.value);

    request.subscribe({
      next: () => {
        this.notification.success(this.isEditMode ? 'Category updated' : 'Category created');
        this.router.navigate(['/categories']);
      },
      error: () => {
        this.saving = false;
      },
    });
  }

  cancel(): void {
    this.router.navigate(['/categories']);
  }
}
