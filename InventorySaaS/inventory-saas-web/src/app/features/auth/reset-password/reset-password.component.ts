import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl } from '@angular/forms';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [CommonModule, RouterModule, ReactiveFormsModule, MatProgressSpinnerModule],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.css',
})
export class ResetPasswordComponent implements OnInit {
  form: FormGroup;
  isLoading = false;
  resetSuccess = false;
  hidePassword = true;
  hideConfirm = true;
  errorMessage = '';

  private email = '';
  private token = '';

  constructor(
    private fb: FormBuilder,
    private route: ActivatedRoute,
    private authService: AuthService,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      newPassword: ['', [Validators.required, Validators.minLength(8)]],
      confirmPassword: ['', [Validators.required]],
    }, { validators: this.passwordMatchValidator });
  }

  ngOnInit(): void {
    this.email = this.route.snapshot.queryParamMap.get('email') ?? '';
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';

    if (!this.email || !this.token) {
      this.errorMessage = 'Invalid or expired reset link. Please request a new one.';
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.isLoading = true;
    this.errorMessage = '';

    this.authService.resetPassword({
      email: this.email,
      token: this.token,
      newPassword: this.form.value.newPassword,
    }).subscribe({
      next: () => {
        this.isLoading = false;
        this.resetSuccess = true;
        this.notification.success('Password reset successfully');
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMessage = err?.error?.errors?.[0] ?? 'Reset link is invalid or expired. Please request a new one.';
      },
    });
  }

  private passwordMatchValidator(group: AbstractControl) {
    const pw = group.get('newPassword')?.value;
    const confirm = group.get('confirmPassword')?.value;
    return pw === confirm ? null : { mismatch: true };
  }
}
