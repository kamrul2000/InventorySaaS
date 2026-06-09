import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NgxChartsModule, Color, ScaleType } from '@swimlane/ngx-charts';
import { DashboardService } from '../../core/services/dashboard.service';
import { AuthService } from '../../core/services/auth.service';
import { DashboardDto, TopProductDto } from '../../core/models/domain.models';

interface ChartDatum { name: string; value: number; }
interface GroupedChartDatum { name: string; series: ChartDatum[]; }

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatProgressSpinnerModule,
    NgxChartsModule,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css',
})
export class DashboardComponent implements OnInit {
  data: DashboardDto | null = null;
  loading = true;
  userName = '';

  // Chart data (built from the dashboard payload).
  topProductsChart: ChartDatum[] = [];
  stockAlertsChart: GroupedChartDatum[] = [];
  financialChart: ChartDatum[] = [];

  readonly chartScheme: Color = {
    name: 'app',
    selectable: true,
    group: ScaleType.Ordinal,
    domain: ['#e8602c', '#3182ce', '#38a169', '#dd6b20', '#805ad5', '#319795', '#d53f8c'],
  };
  readonly currencyAxisFormat = (value: number): string =>
    '৳' + new Intl.NumberFormat('en-US', { notation: 'compact', maximumFractionDigits: 1 }).format(value);

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
        this.buildCharts(data);
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  private buildCharts(data: DashboardDto): void {
    this.topProductsChart = (data.topProducts ?? [])
      .slice(0, 7)
      .map((p) => ({ name: p.productName, value: p.totalValue }));

    this.stockAlertsChart = (data.stockAlerts ?? [])
      .slice(0, 7)
      .map((a) => ({
        name: a.productName,
        series: [
          { name: 'Current', value: a.currentStock },
          { name: 'Reorder', value: a.reorderLevel },
        ],
      }));

    this.financialChart = [
      { name: 'Sales', value: data.totalSales ?? 0 },
      { name: 'Purchases', value: data.totalPurchases ?? 0 },
      { name: 'Inventory Value', value: data.totalInventoryValue ?? 0 },
    ].filter((d) => d.value > 0);
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
