import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { TenantService } from '../../core/services/tenant.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatCardModule, MatFormFieldModule,
    MatInputModule, MatButtonModule, MatProgressSpinnerModule,
  ],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.css',
})
export class SettingsComponent implements OnInit {
  form: FormGroup;
  loading = true;
  saving = false;

  constructor(
    private fb: FormBuilder,
    private tenantService: TenantService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      contactEmail: [''],
      phone: [''],
      address: [''],
      city: [''],
      country: [''],
      currency: [''],
      timezone: [''],
      logoUrl: [''],
    });
  }

  ngOnInit(): void {
    this.tenantService.getCurrent().subscribe({
      next: (tenant) => {
        this.form.patchValue(tenant);
        this.loading = false;
      },
      error: () => { this.loading = false; },
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    this.tenantService.update(this.form.value).subscribe({
      next: () => {
        this.notification.success('Settings saved');
        this.saving = false;
      },
      error: () => { this.saving = false; },
    });
  }
}
