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
import { UserService } from '../../../core/services/user.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-user-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, MatCardModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatCheckboxModule, MatButtonModule, MatProgressSpinnerModule,
  ],
  template: `
    <div class="form-container">
      <mat-card>
        <mat-card-header>
          <mat-card-title>{{ isEditMode ? 'Edit User' : 'New User' }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Email</mat-label>
                <input matInput formControlName="email" type="email">
                @if (form.get('email')?.hasError('required') && form.get('email')?.touched) { <mat-error>Required</mat-error> }
                @if (form.get('email')?.hasError('email') && form.get('email')?.touched) { <mat-error>Invalid email</mat-error> }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>First Name</mat-label>
                <input matInput formControlName="firstName">
                @if (form.get('firstName')?.hasError('required') && form.get('firstName')?.touched) { <mat-error>Required</mat-error> }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Last Name</mat-label>
                <input matInput formControlName="lastName">
                @if (form.get('lastName')?.hasError('required') && form.get('lastName')?.touched) { <mat-error>Required</mat-error> }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Phone</mat-label>
                <input matInput formControlName="phone">
              </mat-form-field>

              @if (!isEditMode) {
                <mat-form-field appearance="outline">
                  <mat-label>Password</mat-label>
                  <input matInput formControlName="password" type="password">
                  @if (form.get('password')?.hasError('required') && form.get('password')?.touched) { <mat-error>Required</mat-error> }
                  @if (form.get('password')?.hasError('minlength') && form.get('password')?.touched) { <mat-error>Min 8 characters</mat-error> }
                </mat-form-field>
              }

              <mat-form-field appearance="outline">
                <mat-label>Roles</mat-label>
                <mat-select formControlName="roles" multiple>
                  <mat-option value="TenantAdmin">Tenant Admin</mat-option>
                  <mat-option value="WarehouseManager">Warehouse Manager</mat-option>
                  <mat-option value="InventoryStaff">Inventory Staff</mat-option>
                  <mat-option value="Viewer">Viewer</mat-option>
                </mat-select>
                @if (form.get('roles')?.hasError('required') && form.get('roles')?.touched) { <mat-error>Select at least one role</mat-error> }
              </mat-form-field>
            </div>

            @if (isEditMode) {
              <mat-checkbox formControlName="isActive">Active</mat-checkbox>
            }

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
    .form-container { max-width: 700px; margin: 0 auto; }
    .form-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); gap: 0 16px; }
    .form-grid mat-form-field { width: 100%; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; }
  `],
})
export class UserFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  userId: string | null = null;

  constructor(
    private fb: FormBuilder, private userService: UserService,
    private router: Router, private route: ActivatedRoute, private notification: NotificationService
  ) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]],
      firstName: ['', [Validators.required]],
      lastName: ['', [Validators.required]],
      phone: [''],
      password: ['', [Validators.required, Validators.minLength(8)]],
      roles: [[], [Validators.required]],
      isActive: [true],
    });
  }

  ngOnInit(): void {
    this.userId = this.route.snapshot.paramMap.get('id');
    if (this.userId) {
      this.isEditMode = true;
      this.form.get('password')?.clearValidators();
      this.form.get('password')?.updateValueAndValidity();
      this.form.get('email')?.disable();
      this.userService.getById(this.userId).subscribe({
        next: (user) => this.form.patchValue(user),
      });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    const data = this.form.getRawValue();

    if (this.isEditMode) {
      this.userService.update(this.userId!, {
        firstName: data.firstName, lastName: data.lastName,
        phone: data.phone, isActive: data.isActive, roles: data.roles,
      }).subscribe({
        next: () => { this.notification.success('User updated'); this.router.navigate(['/users']); },
        error: () => { this.saving = false; },
      });
    } else {
      this.userService.create(data).subscribe({
        next: () => { this.notification.success('User created'); this.router.navigate(['/users']); },
        error: () => { this.saving = false; },
      });
    }
  }

  cancel(): void { this.router.navigate(['/users']); }
}
