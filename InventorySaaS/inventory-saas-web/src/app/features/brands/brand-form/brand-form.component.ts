import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { BrandService } from '../../../core/services/brand.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BrandDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-brand-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
  ],
  templateUrl: './brand-form.component.html',
  styleUrl: './brand-form.component.css',
})
export class BrandFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<BrandFormComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { brand?: BrandDto },
    private brandService: BrandService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      logoUrl: [''],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    if (this.data.brand) {
      this.isEditMode = true;
      this.form.patchValue(this.data.brand);
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const request = this.isEditMode
      ? this.brandService.update(this.data.brand!.id, this.form.value)
      : this.brandService.create(this.form.value);

    request.subscribe({
      next: (brand) => {
        this.notification.success(this.isEditMode ? 'Brand updated' : 'Brand created');
        this.dialogRef.close(brand);
      },
      error: () => { this.saving = false; },
    });
  }
}
