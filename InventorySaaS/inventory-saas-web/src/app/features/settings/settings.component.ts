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
  template: `
    <div class="form-container">
      <h1>Settings</h1>
      <mat-card>
        <mat-card-header>
          <mat-card-title>Company Settings</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          @if (loading) {
            <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
          } @else {
            <form [formGroup]="form" (ngSubmit)="onSubmit()">
              <div class="form-grid">
                <mat-form-field appearance="outline">
                  <mat-label>Company Name</mat-label>
                  <input matInput formControlName="name">
                  @if (form.get('name')?.hasError('required') && form.get('name')?.touched) { <mat-error>Required</mat-error> }
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Contact Email</mat-label>
                  <input matInput formControlName="contactEmail" type="email">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Phone</mat-label>
                  <input matInput formControlName="phone">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Address</mat-label>
                  <input matInput formControlName="address">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>City</mat-label>
                  <input matInput formControlName="city">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Country</mat-label>
                  <input matInput formControlName="country">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Currency</mat-label>
                  <input matInput formControlName="currency" placeholder="USD">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Timezone</mat-label>
                  <input matInput formControlName="timezone" placeholder="UTC">
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Logo URL</mat-label>
                  <input matInput formControlName="logoUrl">
                </mat-form-field>
              </div>

              <div class="form-actions">
                <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving">
                  @if (saving) { <mat-spinner diameter="20"></mat-spinner> } @else { Save Settings }
                </button>
              </div>
            </form>
          }
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .form-container { max-width: 800px; margin: 0 auto; }
    h1 { margin-bottom: 24px; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 0 16px; }
    .form-grid mat-form-field { width: 100%; }
    .form-actions { display: flex; justify-content: flex-end; margin-top: 24px; }
    .loading-center { display: flex; justify-content: center; padding: 48px; }
  `],
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
