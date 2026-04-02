import { Component, EventEmitter, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatBadgeModule } from '@angular/material/badge';
import { MatMenuModule } from '@angular/material/menu';
import { MatDivider } from '@angular/material/divider';
import { AuthService } from '../../core/services/auth.service';
import { NotificationApiService } from '../../core/services/notification-api.service';
import { User } from '../../core/models/auth.models';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatBadgeModule,
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

  constructor(
    private authService: AuthService,
    private notificationApiService: NotificationApiService
  ) {}

  ngOnInit(): void {
    this.authService.currentUser$.subscribe((user) => {
      this.currentUser = user;
    });
    this.loadUnreadCount();
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

  logout(): void {
    this.authService.logout();
  }
}
