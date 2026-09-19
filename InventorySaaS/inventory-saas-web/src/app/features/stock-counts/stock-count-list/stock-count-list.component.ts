import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { HttpErrorResponse } from '@angular/common/http';
import { StockCountService } from '../../../core/services/stock-count.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import {
  StockCountLineDto,
  StockCountSessionDto,
  StockCountStatus,
} from '../../../core/models/stock-count.models';
import { describeApiError } from '../../scan/scan-error';

/** Roles the API's ManagerUp policy admits; approval is hidden from anyone else. */
const MANAGER_ROLES = ['TenantAdmin', 'Manager', 'SuperAdmin'];

@Component({
  selector: 'app-stock-count-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, MatIconModule],
  templateUrl: './stock-count-list.component.html',
  styleUrl: './stock-count-list.component.css',
})
export class StockCountListComponent {
  private readonly stockCountService = inject(StockCountService);
  private readonly notification = inject(NotificationService);
  private readonly auth = inject(AuthService);

  readonly counts = signal<StockCountSessionDto[]>([]);
  readonly selected = signal<StockCountSessionDto | null>(null);

  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  /** Approval is a Manager gate on the API; mirroring it here keeps the UI honest. */
  readonly canApprove = MANAGER_ROLES.some((role) => this.auth.getUserRoles().includes(role));

  statusFilter: StockCountStatus | '' = 'PendingApproval';
  approvalNotes = '';

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);

    this.stockCountService
      .getAll({ pageSize: 50, status: this.statusFilter || undefined })
      .subscribe({
        next: (result) => {
          this.loading.set(false);
          this.counts.set(result.items);

          // Keep the open detail in sync, or drop it if it left the filter.
          const current = this.selected();
          if (current) {
            this.selected.set(result.items.find((c) => c.id === current.id) ?? null);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.loading.set(false);
          this.error.set(describeApiError(error));
        },
      });
  }

  select(count: StockCountSessionDto): void {
    this.approvalNotes = '';
    this.selected.set(count);
  }

  approve(): void {
    const current = this.selected();
    if (!current || this.busy()) return;

    this.busy.set(true);
    this.error.set(null);

    this.stockCountService.approve(current.id, this.approvalNotes.trim() || undefined).subscribe({
      next: (session) => {
        this.busy.set(false);
        this.selected.set(session);
        this.notification.success(
          `${session.countNumber} approved — ${this.adjustedLineCount(session)} adjustment(s) posted`
        );
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeApiError(error));
      },
    });
  }

  reject(): void {
    const current = this.selected();
    if (!current || this.busy()) return;

    this.busy.set(true);

    this.stockCountService.cancel(current.id).subscribe({
      next: () => {
        this.busy.set(false);
        this.notification.success(`${current.countNumber} rejected — no stock was changed`);
        this.selected.set(null);
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.error.set(describeApiError(error));
      },
    });
  }

  /** Lines that disagree with the system, i.e. the ones approval would actually adjust. */
  adjustedLineCount(session: StockCountSessionDto): number {
    return session.lines.filter((l) => l.variance !== 0).length;
  }

  /** Variances first — those are what a reviewer is here to look at. */
  orderedLines(session: StockCountSessionDto): StockCountLineDto[] {
    return [...session.lines].sort((a, b) => {
      const aVaries = a.variance !== 0;
      const bVaries = b.variance !== 0;
      if (aVaries !== bVaries) return aVaries ? -1 : 1;
      return Math.abs(b.variance) - Math.abs(a.variance);
    });
  }
}
