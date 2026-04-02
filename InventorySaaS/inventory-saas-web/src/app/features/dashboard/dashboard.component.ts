import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardDto, TopProductDto } from '../../core/models/domain.models';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatProgressSpinnerModule,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  data: DashboardDto | null = null;
  loading = true;
  userName = '';

  constructor(
    private dashboardService: DashboardService,
    private authService: AuthService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe((user) => {
      this.userName = user?.firstName ?? 'User';
    });

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

  getTxIcon(type: string): string {
    switch (type.toLowerCase()) {
      case 'stockin': return 'arrow_downward';
      case 'stockout': return 'arrow_upward';
      case 'transfer': return 'swap_horiz';
      case 'adjustment': return 'tune';
      default: return 'sync';
    }
  }

  getStockPercent(p: TopProductDto): number {
    if (!this.data || !this.data.topProducts.length) return 0;
    const max = Math.max(...this.data.topProducts.map(tp => tp.totalQuantity));
    return max > 0 ? Math.round((p.totalQuantity / max) * 100) : 0;
  }
}
