import { CommonModule } from '@angular/common';
import { Component, Inject, OnInit, Optional } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { UnitOfMeasureService } from '../../../core/services/unit-of-measure.service';
import { NotificationService } from '../../../core/services/notification.service';
import { UnitOfMeasureDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-unit-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, MatDialogModule, MatIconModule],
  templateUrl: './unit-form.component.html',
  styleUrl: './unit-form.component.css',
})
export class UnitFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  unitId: string | null = null;
  readonly isDialogMode: boolean;

  constructor(
    private fb: FormBuilder,
    private unitService: UnitOfMeasureService,
    private notification: NotificationService,
    private router: Router,
    private route: ActivatedRoute,
    @Optional() public dialogRef: MatDialogRef<UnitFormComponent> | null,
    @Optional() @Inject(MAT_DIALOG_DATA) public data: { unit?: UnitOfMeasureDto } | null,
  ) {
    this.isDialogMode = !!dialogRef;
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      abbreviation: [''],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    const dialogUnit = this.data?.unit;
    this.unitId = dialogUnit?.id ?? this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.unitId;

    if (dialogUnit) {
      this.form.patchValue(dialogUnit);
    } else if (this.unitId) {
      this.unitService.getById(this.unitId).subscribe({
        next: (unit) => this.form.patchValue(unit),
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
      ? this.unitService.update(this.unitId!, this.form.value)
      : this.unitService.create(this.form.value);

    request.subscribe({
      next: (unit) => {
        this.notification.success(this.isEditMode ? 'Unit updated' : 'Unit created');
        if (this.dialogRef) this.dialogRef.close(unit);
        else this.router.navigate(['/units']);
      },
      error: () => {
        this.saving = false;
      },
    });
  }

  cancel(): void {
    if (this.dialogRef) this.dialogRef.close();
    else this.router.navigate(['/units']);
  }
}
