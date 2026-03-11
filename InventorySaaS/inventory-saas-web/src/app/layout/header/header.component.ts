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
  template: `
    <mat-toolbar color="primary">
      <button mat-icon-button (click)="toggleSidenav.emit()">
        <mat-icon>menu</mat-icon>
      </button>
      <span class="app-title">InventorySaaS</span>
      <span class="spacer"></span>

      <button mat-icon-button [routerLink]="'/notifications'"
              [matBadge]="unreadCount > 0 ? unreadCount : null"
              matBadgeColor="warn"
              matBadgeSize="small">
        <mat-icon>notifications</mat-icon>
      </button>

      <button mat-icon-button [matMenuTriggerFor]="userMenu">
        <mat-icon>account_circle</mat-icon>
      </button>
      <mat-menu #userMenu="matMenu">
        <div class="user-info" mat-menu-item disabled>
          <strong>{{ currentUser?.firstName }} {{ currentUser?.lastName }}</strong>
          <br>
          <small>{{ currentUser?.email }}</small>
        </div>
        <mat-divider></mat-divider>
        <button mat-menu-item routerLink="/settings">
          <mat-icon>settings</mat-icon>
          <span>Settings</span>
        </button>
        <button mat-menu-item (click)="logout()">
          <mat-icon>logout</mat-icon>
          <span>Logout</span>
        </button>
      </mat-menu>
    </mat-toolbar>
  `,
  styles: [`
    .spacer {
      flex: 1 1 auto;
    }
    .app-title {
      margin-left: 8px;
      font-size: 1.1rem;
    }
    .user-info {
      line-height: 1.4;
      white-space: normal;
    }
  `],
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
