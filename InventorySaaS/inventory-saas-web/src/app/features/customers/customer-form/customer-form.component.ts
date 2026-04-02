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
  templateUrl: './customer-form.component.html',
  styleUrl: './customer-form.component.css',
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
