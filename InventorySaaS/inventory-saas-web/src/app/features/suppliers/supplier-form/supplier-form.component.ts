import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute, RouterModule } from '@angular/router';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { SupplierService } from '../../../core/services/supplier.service';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
  selector: 'app-supplier-form',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, RouterModule, MatIconModule,
  ],
  templateUrl: './supplier-form.component.html',
  styleUrl: './supplier-form.component.css',
})
export class SupplierFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  supplierId: string | null = null;

  constructor(
    private fb: FormBuilder, private supplierService: SupplierService,
    private router: Router, private route: ActivatedRoute, private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]], code: [''], contactPerson: [''], email: [''],
      phone: [''], address: [''], city: [''], country: [''], isActive: [true],
    });
  }

  ngOnInit(): void {
    this.supplierId = this.route.snapshot.paramMap.get('id');
    if (this.supplierId) {
      this.isEditMode = true;
      this.supplierService.getById(this.supplierId).subscribe({ next: (s) => this.form.patchValue(s) });
    }
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.saving = true;
    const req = this.isEditMode
      ? this.supplierService.update(this.supplierId!, this.form.value)
      : this.supplierService.create(this.form.value);
    req.subscribe({
      next: () => { this.notification.success(this.isEditMode ? 'Updated' : 'Created'); this.router.navigate(['/suppliers']); },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void { this.router.navigate(['/suppliers']); }
}
