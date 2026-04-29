import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService, Toast, ToastVariant } from '../../core/services/notification.service';

@Component({
  selector: 'app-toast-container',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './toast-container.component.html',
  styleUrl: './toast-container.component.css',
})
export class ToastContainerComponent {
  private readonly service = inject(NotificationService);
  readonly toasts = this.service.toasts;

  dismiss(id: number): void {
    this.service.dismiss(id);
  }

  iconFor(variant: ToastVariant): string {
    switch (variant) {
      case 'success':
        return '✓';
      case 'error':
        return '✕';
      case 'warning':
        return '!';
      default:
        return 'i';
    }
  }

  timeAgo(toast: Toast): string {
    const ms = Date.now() - toast.createdAt.getTime();
    if (ms < 60_000) return 'just now';
    const minutes = Math.floor(ms / 60_000);
    return `${minutes} min${minutes === 1 ? '' : 's'} ago`;
  }
}
