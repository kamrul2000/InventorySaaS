import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { NotificationService } from '../../../core/services/notification.service';
import { WarehouseLocationDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-warehouse-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './warehouse-form.component.html',
  styleUrl: './warehouse-form.component.css',
})
export class WarehouseFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  warehouseId: string | null = null;
  locations: WarehouseLocationDto[] = [];
  showLocationForm = false;
  newLocation = { name: '', aisle: '', rack: '', bin: '' };

  constructor(
    private fb: FormBuilder,
    private warehouseService: WarehouseService,
    private router: Router,
    private route: ActivatedRoute,
    private notification: NotificationService
  ) {
    this.form = this.fb.group({
      name: ['', [Validators.required]],
      code: ['', [Validators.required]],
      address: [''],
      city: [''],
      country: [''],
      contactPerson: [''],
      phone: [''],
      isDefault: [false],
    });
  }

  ngOnInit(): void {
    this.warehouseId = this.route.snapshot.paramMap.get('id');
    if (this.warehouseId) {
      this.isEditMode = true;
      this.warehouseService.getById(this.warehouseId).subscribe({
        next: (wh) => this.form.patchValue(wh),
      });
      this.warehouseService.getLocations(this.warehouseId).subscribe({
        next: (locs) => this.locations = locs,
      });
    }
  }

  addLocation(): void {
    this.showLocationForm = true;
    this.newLocation = { name: '', aisle: '', rack: '', bin: '' };
  }

  saveLocation(): void {
    if (!this.newLocation.name || !this.warehouseId) return;
    this.warehouseService.createLocation(this.warehouseId, this.newLocation).subscribe({
      next: (loc) => {
        this.locations = [...this.locations, loc];
        this.showLocationForm = false;
        this.notification.success('Location added');
      },
    });
  }

  onSubmit(): void {
    if (this.form.invalid) return;
    this.saving = true;

    const request = this.isEditMode
      ? this.warehouseService.update(this.warehouseId!, this.form.value)
      : this.warehouseService.create(this.form.value);

    request.subscribe({
      next: () => {
        this.notification.success(this.isEditMode ? 'Warehouse updated' : 'Warehouse created');
        this.router.navigate(['/warehouses']);
      },
      error: () => { this.saving = false; },
    });
  }

  cancel(): void {
    this.router.navigate(['/warehouses']);
  }
}
