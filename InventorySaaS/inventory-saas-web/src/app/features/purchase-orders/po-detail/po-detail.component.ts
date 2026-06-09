import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PurchaseOrderDto } from '../../../core/models/domain.models';

@Component({
  selector: 'app-po-detail',
  standalone: true,
  imports: [
    CommonModule, MatIconModule, MatProgressSpinnerModule,
  ],
  templateUrl: './po-detail.component.html',
  styleUrl: './po-detail.component.css',
})
export class PoDetailComponent implements OnInit {
  order: PurchaseOrderDto | null = null;
  loading = true;
  itemColumns = ['productName', 'productSku', 'quantity', 'receivedQuantity', 'unitPrice', 'lineTotal'];

  constructor(
    private poService: PurchaseOrderService, private route: ActivatedRoute,
    private router: Router, private notification: NotificationService
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.poService.getById(id).subscribe({
        next: (order) => { this.order = order; this.loading = false; },
        error: () => { this.loading = false; },
      });
    }
  }

  approve(): void {
    if (!this.order) return;
    this.poService.approve(this.order.id).subscribe({
      next: () => { this.notification.success('Purchase order approved'); this.ngOnInit(); },
    });
  }

  receiveGoods(): void {
    if (!this.order) return;
    const data = {
      purchaseOrderId: this.order.id,
      items: this.order.items.map((item: any) => ({
        productId: item.productId,
        quantity: item.quantity - (item.receivedQuantity || 0),
        rejectedQuantity: 0,
        locationId: null,
        batchNumber: null,
        lotNumber: null,
        expiryDate: null,
      })),
      notes: null,
    };
    this.poService.receiveGoods(this.order.id, data).subscribe({
      next: () => { this.notification.success('Goods received'); this.ngOnInit(); },
    });
  }

  get canReturn(): boolean {
    if (!this.order) return false;
    if (this.order.status !== 'Received' && this.order.status !== 'PartiallyReceived') return false;
    return this.order.items.some(
      (item: any) => (item.receivedQuantity || 0) - (item.returnedQuantity || 0) > 0
    );
  }

  returnGoods(): void {
    if (!this.order) return;
    if (!confirm('Return all outstanding received goods to the supplier? Stock on hand will be reduced.')) return;
    const data = {
      purchaseOrderId: this.order.id,
      items: this.order.items
        .map((item: any) => ({
          productId: item.productId,
          quantity: (item.receivedQuantity || 0) - (item.returnedQuantity || 0),
          reason: null,
        }))
        .filter((item) => item.quantity > 0),
      reason: null,
    };
    this.poService.returnGoods(this.order.id, data).subscribe({
      next: () => { this.notification.success('Goods returned to supplier'); this.ngOnInit(); },
    });
  }

  back(): void { this.router.navigate(['/purchase-orders']); }
}
