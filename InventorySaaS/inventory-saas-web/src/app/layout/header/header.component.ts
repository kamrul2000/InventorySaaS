import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatDivider } from '@angular/material/divider';
import { AuthService } from '../../core/services/auth.service';
import { NotificationApiService } from '../../core/services/notification-api.service';
import { User } from '../../core/models/auth.models';
import { NotificationDto } from '../../core/models/domain.models';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatIconModule,
    MatMenuModule,
    MatDivider,
  ],
  templateUrl: './header.component.html',
  styleUrl: './header.component.css',
})
export class HeaderComponent implements OnInit {
  @Output() toggleSidenav = new EventEmitter<void>();

  currentUser: User | null = null;
  unreadCount = 0;
  recentNotifications: NotificationDto[] = [];

  constructor(
    private authService: AuthService,
    private notificationApiService: NotificationApiService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe((user) => {
      this.currentUser = user;
    });
    this.loadUnreadCount();
    this.loadRecentNotifications();
  }

  get userInitials(): string {
    const f = this.currentUser?.firstName?.[0] ?? '';
    const l = this.currentUser?.lastName?.[0] ?? '';
    return (f + l).toUpperCase() || 'U';
  }

  get primaryRole(): string {
    const roles = this.currentUser?.roles ?? [];
    if (roles.includes('SuperAdmin')) return 'Super Admin';
    if (roles.includes('TenantAdmin')) return 'Admin';
    return roles[0] ?? 'User';
  }

  loadUnreadCount(): void {
    this.notificationApiService.getAll({ pageNumber: 1, pageSize: 1, isRead: false }).subscribe({
      next: (result) => {
        this.unreadCount = result.totalCount;
      },
      error: () => {
        this.unreadCount = 0;
      },
    });
  }

  loadRecentNotifications(): void {
    this.notificationApiService.getAll({ pageNumber: 1, pageSize: 5 }).subscribe({
      next: (result) => {
        this.recentNotifications = result.items;
      },
      error: () => {
        this.recentNotifications = [];
      },
    });
  }

  onNotifOpened(): void {
    this.loadRecentNotifications();
  }

  onNotifClick(n: NotificationDto): void {
    if (!n.isRead) {
      this.notificationApiService.markRead(n.id).subscribe(() => {
        n.isRead = true;
        this.unreadCount = Math.max(0, this.unreadCount - 1);
      });
    }
  }

  notifIcon(type: string): string {
    switch (type) {
      case 'LowStock': return 'warning';
      case 'Expiry': return 'event_busy';
      case 'Order': return 'shopping_cart';
      default: return 'info';
    }
  }

  timeAgo(dateStr: string): string {
    const now = new Date();
    const date = new Date(dateStr);
    const diffMs = now.getTime() - date.getTime();
    const mins = Math.floor(diffMs / 60000);
    if (mins < 1) return 'Just now';
    if (mins < 60) return `${mins} minute${mins === 1 ? '' : 's'} ago`;
    const hours = Math.floor(mins / 60);
    if (hours < 24) return `${hours} hour${hours === 1 ? '' : 's'} ago`;
    const days = Math.floor(hours / 24);
    if (days < 7) return `${days} day${days === 1 ? '' : 's'} ago`;
    return date.toLocaleDateString();
  }

  markAllRead(): void {
    this.notificationApiService.markAllRead().subscribe(() => {
      this.recentNotifications.forEach(n => n.isRead = true);
      this.unreadCount = 0;
    });
  }

  logout(): void {
    this.authService.logout();
  }
}
