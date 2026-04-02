import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { NotificationService } from '../../../core/services/notification.service';
import { SalesOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-so-detail',
  standalone: true,
  imports: [
    CommonModule, MatCardModule, MatTableModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule,
  ],
  templateUrl: './so-detail.component.html',
  styleUrl: './so-detail.component.css',
})
export class SoDetailComponent implements OnInit {
  order: SalesOrderDto | null = null;
  loading = true;
  itemColumns = ['productName', 'productSku', 'quantity', 'deliveredQuantity', 'unitPrice', 'lineTotal'];

  constructor(
    private soService: SalesOrderService, private route: ActivatedRoute,
    private router: Router, private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.soService.getById(id).subscribe({
        next: (order) => { this.order = order; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  confirm(): void {
    if (!this.order) return;
    this.soService.confirm(this.order.id).subscribe({
      next: () => { this.notification.success('Sales order confirmed'); this.ngOnInit(); },
    });
  }

  deliver(): void {
    if (!this.order) return;
    const data = {
      salesOrderId: this.order.id,
      items: this.order.items.map((item: any) => ({
        productId: item.productId,
        quantity: item.quantity - (item.deliveredQuantity || 0),
      })),
      notes: null,
    };
    this.soService.deliver(this.order.id, data).subscribe({
      next: () => { this.notification.success('Order delivered'); this.ngOnInit(); },
    });
  }

  back(): void { this.router.navigate(['/sales-orders']); }
}
