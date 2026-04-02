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
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.css',
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
