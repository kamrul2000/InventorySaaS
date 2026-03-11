import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { NotificationApiService } from '../../core/services/notification-api.service';
import { NotificationService } from '../../core/services/notification.service';
import { NotificationDto } from '../../core/models/domain.models';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [CommonModule, MatListModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule],
  template: `
    <div class="page-header">
      <h1>Notifications</h1>
      <button mat-stroked-button color="primary" (click)="markAllRead()" [disabled]="notifications.length === 0">
        <mat-icon>done_all</mat-icon> Mark All as Read
      </button>
    </div>

    @if (loading) {
      <div class="loading-center"><mat-spinner diameter="40"></mat-spinner></div>
    } @else {
      @if (notifications.length === 0) {
        <div class="empty-state">
          <mat-icon>notifications_none</mat-icon>
          <p>No notifications</p>
        </div>
      } @else {
        <mat-list>
          @for (n of notifications; track n.id) {
            <mat-list-item [class.unread]="!n.isRead" (click)="markRead(n)">
              <mat-icon matListItemIcon>{{ getIcon(n.type) }}</mat-icon>
              <div matListItemTitle>{{ n.title }}</div>
              <div matListItemLine>{{ n.message }}</div>
              <div matListItemMeta>{{ n.createdAt | date:'short' }}</div>
            </mat-list-item>
          }
        </mat-list>
      }
    }
  `,
  styles: [`
    .page-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px; }
    .page-header h1 { margin: 0; }
    .loading-center { display: flex; justify-content: center; padding: 48px; }
    .empty-state { text-align: center; padding: 48px; color: rgba(0,0,0,.38); }
    .empty-state mat-icon { font-size: 48px; width: 48px; height: 48px; }
    .unread { background-color: var(--mat-sys-primary-container); border-radius: 8px; margin-bottom: 4px; }
  `],
})
export class NotificationsComponent implements OnInit {
  notifications: NotificationDto[] = [];
  loading = true;

  constructor(
    private notificationApiService: NotificationApiService,
    private notification: NotificationService
  ) {}

  ngOnInit(): void {
    this.loadNotifications();
  }

  loadNotifications(): void {
    this.loading = true;
    this.notificationApiService.getAll({ pageSize: 50 }).subscribe({
      next: (result) => { this.notifications = result.items; this.loading = false; },
      error: () => { this.loading = false; },
    });
  }

  markRead(n: NotificationDto): void {
    if (n.isRead) return;
    this.notificationApiService.markRead(n.id).subscribe({
      next: () => { n.isRead = true; },
    });
  }

  markAllRead(): void {
    this.notificationApiService.markAllRead().subscribe({
      next: () => {
        this.notifications.forEach((n) => n.isRead = true);
        this.notification.success('All notifications marked as read');
      },
    });
  }

  getIcon(type: string): string {
    const icons: Record<string, string> = {
      'LowStock': 'warning',
      'Expiry': 'event_busy',
      'Order': 'shopping_cart',
      'System': 'info',
    };
    return icons[type] || 'notifications';
  }
}
