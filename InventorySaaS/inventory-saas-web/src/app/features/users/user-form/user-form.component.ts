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
  templateUrl: './user-form.component.html',
  styleUrl: './user-form.component.css',
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
