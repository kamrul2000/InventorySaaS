import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { PurchaseOrderService } from '../../../core/services/purchase-order.service';
import { SalesOrderService } from '../../../core/services/sales-order.service';
import { SupplierBillService } from '../../../core/services/supplier-bill.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { STAFF_UP, MANAGER_UP } from '../../../core/constants/roles';
import { PaginatedList } from '../../../core/models/api.models';

const EMPTY_PAGE: PaginatedList<never> = {
  items: [], pageNumber: 1, totalPages: 0, totalCount: 0, hasPreviousPage: false, hasNextPage: false,
};

/**
 * A pending approval, normalized across the three document types that gate a status transition
 * behind a role. There is no assigned "next approver" in the data model — any user holding
 * `requiredRoleLabel` may act — so the queue surfaces the role tier required instead of a name.
 */
interface QueueItem {
  type: 'Purchase Order' | 'Sales Order' | 'Supplier Bill';
  id: string;
  number: string;
  party: string;
  date: string;
  amount: number;
  requiredRoles: string[];
  requiredRoleLabel: string;
  actionLabel: string;
  detailRoute: string[];
  act: () => void;
}

@Component({
  selector: 'app-approval-queue',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './approval-queue.component.html',
  styleUrl: './approval-queue.component.css',
})
export class ApprovalQueueComponent implements OnInit {
  items: QueueItem[] = [];
  loading = true;
  acting: string | null = null;

  constructor(
    private poService: PurchaseOrderService,
    private soService: SalesOrderService,
    private billService: SupplierBillService,
    private notification: NotificationService,
    private authService: AuthService,
    private router: Router,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  canAct(item: QueueItem): boolean {
    return this.authService.hasAnyRole(item.requiredRoles);
  }

  load(): void {
    this.loading = true;
    // Each source is individually fault-tolerant so one document type being unavailable (a
    // temporary 500, or a role restriction tighter than this queue's own STAFF_UP guard) only
    // drops that type from the queue instead of emptying the whole thing - forkJoin as a whole
    // errors, and discards every already-fetched result, the moment any ONE source errors (FE-04).
    forkJoin({
      // PurchaseOrderStatus also has a 'Submitted' value, but nothing in the app ever sets it —
      // every new PO sits in 'Draft' until approved — so a single status filter covers it.
      pos: this.poService.getAll({ pageNumber: 1, pageSize: 50, status: 'Draft', sortBy: 'ordernumber' })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      sos: this.soService.getAll({ pageNumber: 1, pageSize: 50, status: 'Draft', sortBy: 'ordernumber' })
        .pipe(catchError(() => of(EMPTY_PAGE))),
      bills: this.billService.getAll({ pageNumber: 1, pageSize: 50, status: 'Draft', sortBy: 'billnumber' })
        .pipe(catchError(() => of(EMPTY_PAGE))),
    }).subscribe({
      next: ({ pos, sos, bills }) => {
        const poItems: QueueItem[] = pos.items.map((po) => ({
          type: 'Purchase Order',
          id: po.id,
          number: po.orderNumber,
          party: po.supplierName,
          date: po.orderDate,
          amount: po.totalAmount,
          requiredRoles: MANAGER_UP,
          requiredRoleLabel: 'Manager',
          actionLabel: 'Approve',
          detailRoute: ['/purchase-orders', po.id],
          act: () => this.approvePo(po.id),
        }));

        const soItems: QueueItem[] = sos.items.map((so) => ({
          type: 'Sales Order',
          id: so.id,
          number: so.orderNumber,
          party: so.customerName,
          date: so.orderDate,
          amount: so.totalAmount,
          requiredRoles: MANAGER_UP,
          requiredRoleLabel: 'Manager',
          actionLabel: 'Confirm',
          detailRoute: ['/sales-orders', so.id],
          act: () => this.confirmSo(so.id),
        }));

        const billItems: QueueItem[] = bills.items.map((bill) => ({
          type: 'Supplier Bill',
          id: bill.id,
          number: bill.billNumber,
          party: bill.supplierName,
          date: bill.billDate,
          amount: bill.totalAmount,
          requiredRoles: STAFF_UP,
          requiredRoleLabel: 'Staff',
          actionLabel: 'Approve',
          detailRoute: ['/supplier-bills', bill.id],
          act: () => this.approveBill(bill.id),
        }));

        this.items = [...poItems, ...soItems, ...billItems].sort(
          (a, b) => new Date(a.date).getTime() - new Date(b.date).getTime(),
        );
        this.loading = false;
      },
      error: () => { this.loading = false; },
    });
  }

  open(item: QueueItem): void {
    this.router.navigate(item.detailRoute);
  }

  approve(item: QueueItem): void {
    this.acting = item.id;
    item.act();
  }

  private approvePo(id: string): void {
    this.poService.approve(id).subscribe({
      next: () => { this.notification.success('Purchase order approved'); this.afterAction(); },
      error: () => { this.acting = null; },
    });
  }

  private confirmSo(id: string): void {
    this.soService.confirm(id).subscribe({
      next: () => { this.notification.success('Sales order confirmed'); this.afterAction(); },
      error: () => { this.acting = null; },
    });
  }

  private approveBill(id: string): void {
    this.billService.approve(id).subscribe({
      next: () => { this.notification.success('Supplier bill approved'); this.afterAction(); },
      error: () => { this.acting = null; },
    });
  }

  private afterAction(): void {
    this.acting = null;
    this.load();
  }
}
