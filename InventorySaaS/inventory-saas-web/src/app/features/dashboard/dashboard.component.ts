import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardDto } from '../../core/models/domain.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    MatCardModule,
    MatIconModule,
    MatTableModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  data: DashboardDto | null = null;
  loading = true;

  txColumns = ['transactionNumber', 'type', 'productName', 'quantity', 'date'];
  alertColumns = ['productName', 'sku', 'warehouseName', 'currentStock', 'reorderLevel'];

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.get().subscribe({
      next: (data) => {
        this.data = data;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }
}
