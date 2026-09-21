import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { BrandService } from '../../../core/services/brand.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BrandDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-brand-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, MatDialogModule, MatIconModule],
  templateUrl: './brand-form.component.html',
  styleUrl: './brand-form.component.css',
})
export class BrandFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  brandId: string | null = null;
  readonly isDialogMode: boolean;

  constructor(
    private fb: FormBuilder,
    private brandService: BrandService,
    private notification: NotificationService,
    private router: Router,
    private route: ActivatedRoute,
    @Optional() public dialogRef: MatDialogRef<BrandFormComponent> | null,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: { brand?: BrandDto } | null,
  ) {
    this.isDialogMode = !!dialogRef;
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      description: [''],
      logoUrl: [''],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    const dialogBrand = this.data?.brand;
    this.brandId = dialogBrand?.id ?? this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.brandId;

    if (dialogBrand) {
      this.form.patchValue(dialogBrand);
    } else if (this.brandId) {
      this.brandService.getById(this.brandId).subscribe({
        next: (brand) => this.form.patchValue(brand),
      });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving = true;
    const request = this.isEditMode
      ? this.brandService.update(this.brandId!, this.form.value)
      : this.brandService.create(this.form.value);

    request.subscribe({
      next: (brand) => {
        this.notification.success(this.isEditMode ? 'Brand updated' : 'Brand created');
        if (this.dialogRef) this.dialogRef.close(brand);
        else this.router.navigate(['/brands']);
      },
      error: () => {
        this.saving = false;
      },
    });
  }

  cancel(): void {
    if (this.dialogRef) this.dialogRef.close();
    else this.router.navigate(['/brands']);
  }
}
