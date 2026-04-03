import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { CategoryService } from '../../../core/services/category.service';
import { NotificationService } from '../../../core/services/notification.service';
import { CategoryDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-category-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
  ],
  templateUrl: './category-form.component.html',
  styleUrl: './category-form.component.css',
})
export class CategoryFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  parentCategories: CategoryDto[] = [];

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<CategoryFormComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { category?: CategoryDto; categories: CategoryDto[] },
    private categoryService: CategoryService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      parentCategoryId: [null],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    this.parentCategories = this.data.categories || [];
    if (this.data.category) {
      this.isEditMode = true;
      this.form.patchValue(this.data.category);
      this.parentCategories = this.parentCategories.filter((c) => c.id !== this.data.category!.id);
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const request = this.isEditMode
      ? this.categoryService.update(this.data.category!.id, this.form.value)
      : this.categoryService.create(this.form.value);

    request.subscribe({
      next: () => {
        this.notification.success(this.isEditMode ? 'Category updated' : 'Category created');
        this.dialogRef.close(true);
      },
      error: () => { this.saving = false; },
    });
  }
}
