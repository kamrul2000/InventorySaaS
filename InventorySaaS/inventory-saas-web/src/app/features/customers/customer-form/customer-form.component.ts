import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CustomerService } from '../../../core/services/customer.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-customer-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatCardModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatCheckboxModule, MatButtonModule, MatProgressSpinnerModule,
  ],
  template: `
    <div class="form-container">
      <mat-card>
        <mat-card-header>
          <mat-card-title>{{ isEditMode ? 'Edit Customer' : 'New Customer' }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name">
                @if (form.get('name')?.hasError('required') && form.get('name')?.touched) { <mat-error>Required</mat-error> }
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Code</mat-label>
                <input matInput formControlName="code">
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Customer Type</mat-label>
                <mat-select formControlName="customerType">
                  <mat-option value="Individual">Individual</mat-option>
                  <mat-option value="Business">Business</mat-option>
                  <mat-option value="Government">Government</mat-option>
                </mat-select>
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Contact Person</mat-label>
                <input matInput formControlName="contactPerson">
              </mat-form-field>
              <mat-form-field appearance="outline">
                <mat-label>Email</mat-label>
                <input matInput formControlName="email" type="email">
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
            </div>
            <mat-checkbox formControlName="isActive">Active</mat-checkbox>
            <div class="form-actions">
              <button mat-button type="button" (click)="cancel()">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving">
                @if (saving) { <mat-spinner diameter="20"></mat-spinner> } @else { {{ isEditMode ? 'Update' : 'Create' }} }
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .form-container { max-width: 800px; margin: 0 auto; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 0 16px; }
    .form-grid mat-form-field { width: 100%; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; }
  `],
})
export class CustomerFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  customerId: string | null = null;

  constructor(
    private fb: FormBuilder, private customerService: CustomerService,
    private router: Router, private route: ActivatedRoute, private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]], code: [''], customerType: [''], contactPerson: [''],
      email: [''], phone: [''], address: [''], city: [''], country: [''], isActive: [true],
    });
  }

  ngOnInit(): void {
    this.customerId = this.route.snapshot.paramMap.get('id');
    if (this.customerId) {
      this.isEditMode = true;
      this.customerService.getById(this.customerId).subscribe({ next: (c) => this.form.patchValue(c) });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    const req = this.isEditMode
      ? this.customerService.update(this.customerId!, this.form.value)
      : this.customerService.create(this.form.value);
    req.subscribe({
      next: () => { this.notification.success(this.isEditMode ? 'Updated' : 'Created'); this.router.navigate(['/customers']); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/customers']); }
}
