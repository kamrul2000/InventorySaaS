import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { UnitOfMeasureService } from '../../../core/services/unit-of-measure.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UnitOfMeasureDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-unit-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatIconModule,
  ],
  templateUrl: './unit-form.component.html',
  styleUrl: './unit-form.component.css',
})
export class UnitFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;

  constructor(
    private fb: FormBuilder,
    public dialogRef: MatDialogRef<UnitFormComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { unit?: UnitOfMeasureDto },
    private unitService: UnitOfMeasureService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      abbreviation: [''],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    if (this.data.unit) {
      this.isEditMode = true;
      this.form.patchValue(this.data.unit);
    }
  }

  save(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const request = this.isEditMode
      ? this.unitService.update(this.data.unit!.id, this.form.value)
      : this.unitService.create(this.form.value);

    request.subscribe({
      next: (unit) => {
        this.notification.success(this.isEditMode ? 'Unit updated' : 'Unit created');
        this.dialogRef.close(unit);
      },
      error: () => { this.saving = false; },
    });
  }
}
