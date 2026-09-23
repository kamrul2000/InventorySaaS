import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { UnitOfMeasureService } from '../../../core/services/unit-of-measure.service';
import { UnitOfMeasureDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-unit-detail',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './unit-detail.component.html',
  styleUrl: './unit-detail.component.css',
})
export class UnitDetailComponent implements OnInit {
  unit: UnitOfMeasureDto | null = null;
  loading = true;

  constructor(
    private unitService: UnitOfMeasureService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.unitService.getById(id).subscribe({
        next: (u) => { this.unit = u; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  edit(): void { if (this.unit) this.router.navigate(['/units', this.unit.id, 'edit']); }
  back(): void { this.router.navigate(['/units']); }
}
