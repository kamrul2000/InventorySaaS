import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { WarehouseService } from '../../../core/services/warehouse.service';
import { WarehouseDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-warehouse-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './warehouse-detail.component.html',
  styleUrl: './warehouse-detail.component.css',
})
export class WarehouseDetailComponent implements OnInit {
  warehouse: WarehouseDto | null = null;
  loading = true;

  constructor(
    private warehouseService: WarehouseService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.warehouseService.getById(id).subscribe({
        next: (w) => { this.warehouse = w; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.warehouse) this.router.navigate(['/warehouses', this.warehouse.id, 'edit']); }
  back(): void { this.router.navigate(['/warehouses']); }
}
