import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, ActivatedRoute } from '@angular/router';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
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
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
  ],
  template: `
    <div class="form-container">
      <mat-card>
        <mat-card-header>
          <mat-card-title>{{ isEditMode ? 'Edit Warehouse' : 'New Warehouse' }}</mat-card-title>
        </mat-card-header>
        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="onSubmit()">
            <div class="form-grid">
              <mat-form-field appearance="outline">
                <mat-label>Name</mat-label>
                <input matInput formControlName="name">
                @if (form.get('name')?.hasError('required') && form.get('name')?.touched) {
                  <mat-error>Name is required</mat-error>
                }
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Code</mat-label>
                <input matInput formControlName="code">
                @if (form.get('code')?.hasError('required') && form.get('code')?.touched) {
                  <mat-error>Code is required</mat-error>
                }
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
                <mat-label>Contact Person</mat-label>
                <input matInput formControlName="contactPerson">
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Phone</mat-label>
                <input matInput formControlName="phone">
              </mat-form-field>
            </div>

            <div class="checkbox-row">
              <mat-checkbox formControlName="isDefault">Default Warehouse</mat-checkbox>
            </div>

            @if (isEditMode) {
              <div class="locations-section">
                <h3>
                  Locations
                  <button mat-mini-fab color="primary" type="button" (click)="addLocation()">
                    <mat-icon>add</mat-icon>
                  </button>
                </h3>

                @if (showLocationForm) {
                  <div class="location-form">
                    <mat-form-field appearance="outline">
                      <mat-label>Location Name</mat-label>
                      <input matInput [(ngModel)]="newLocation.name" [ngModelOptions]="{standalone: true}">
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Aisle</mat-label>
                      <input matInput [(ngModel)]="newLocation.aisle" [ngModelOptions]="{standalone: true}">
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Rack</mat-label>
                      <input matInput [(ngModel)]="newLocation.rack" [ngModelOptions]="{standalone: true}">
                    </mat-form-field>
                    <mat-form-field appearance="outline">
                      <mat-label>Bin</mat-label>
                      <input matInput [(ngModel)]="newLocation.bin" [ngModelOptions]="{standalone: true}">
                    </mat-form-field>
                    <button mat-flat-button color="accent" type="button" (click)="saveLocation()">Save Location</button>
                    <button mat-button type="button" (click)="showLocationForm = false">Cancel</button>
                  </div>
                }

                @if (locations.length > 0) {
                  <table mat-table [dataSource]="locations" class="full-width">
                    <ng-container matColumnDef="name">
                      <th mat-header-cell *matHeaderCellDef>Name</th>
                      <td mat-cell *matCellDef="let loc">{{ loc.name }}</td>
                    </ng-container>
                    <ng-container matColumnDef="aisle">
                      <th mat-header-cell *matHeaderCellDef>Aisle</th>
                      <td mat-cell *matCellDef="let loc">{{ loc.aisle }}</td>
                    </ng-container>
                    <ng-container matColumnDef="rack">
                      <th mat-header-cell *matHeaderCellDef>Rack</th>
                      <td mat-cell *matCellDef="let loc">{{ loc.rack }}</td>
                    </ng-container>
                    <ng-container matColumnDef="bin">
                      <th mat-header-cell *matHeaderCellDef>Bin</th>
                      <td mat-cell *matCellDef="let loc">{{ loc.bin }}</td>
                    </ng-container>
                    <tr mat-header-row *matHeaderRowDef="locationColumns"></tr>
                    <tr mat-row *matRowDef="let row; columns: locationColumns;"></tr>
                  </table>
                }
              </div>
            }

            <div class="form-actions">
              <button mat-button type="button" (click)="cancel()">Cancel</button>
              <button mat-flat-button color="primary" type="submit" [disabled]="form.invalid || saving">
                @if (saving) {
                  <mat-spinner diameter="20"></mat-spinner>
                } @else {
                  {{ isEditMode ? 'Update' : 'Create' }}
                }
              </button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: [`
    .form-container { max-width: 800px; margin: 0 auto; }
    .form-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 0 16px;
    }
    .form-grid mat-form-field { width: 100%; }
    .checkbox-row { margin: 16px 0; }
    .form-actions { display: flex; justify-content: flex-end; gap: 8px; margin-top: 24px; }
    .locations-section { margin-top: 24px; }
    .locations-section h3 { display: flex; align-items: center; gap: 12px; }
    .location-form {
      display: flex; flex-wrap: wrap; gap: 8px; align-items: center; margin-bottom: 16px;
    }
    .full-width { width: 100%; }
  `],
})
export class WarehouseFormComponent implements OnInit {
  form: FormGroup;
  isEditMode = false;
  saving = false;
  warehouseId: string | null = null;
  locations: WarehouseLocationDto[] = [];
  locationColumns = ['name', 'aisle', 'rack', 'bin'];
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
