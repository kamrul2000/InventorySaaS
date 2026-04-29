import { Injectable, signal } from '@angular/core';

export type ToastVariant = 'success' | 'error' | 'warning' | 'info';

export interface Toast {
  id: number;
  variant: ToastVariant;
  title: string;
  message: string;
  createdAt: Date;
  duration: number;
}

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 1;
  private timers = new Map<number, ReturnType<typeof setTimeout>>();

  readonly toasts = signal<Toast[]>([]);

  success(message: string, title = 'Success'): void {
    this.show({ variant: 'success', title, message, duration: 3000 });
  }

  error(message: string, title = 'Error'): void {
    this.show({ variant: 'error', title, message, duration: 5000 });
  }

  warning(message: string, title = 'Warning'): void {
    this.show({ variant: 'warning', title, message, duration: 4000 });
  }

  info(message: string, title = 'Info'): void {
    this.show({ variant: 'info', title, message, duration: 3000 });
  }

  dismiss(id: number): void {
    const timer = this.timers.get(id);
    if (timer) {
      clearTimeout(timer);
      this.timers.delete(id);
    }
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(opts: Omit<Toast, 'id' | 'createdAt'>): void {
    const toast: Toast = {
      id: this.nextId++,
      createdAt: new Date(),
      ...opts,
    };
    this.toasts.update((list) => [...list, toast]);
    const timer = setTimeout(() => this.dismiss(toast.id), opts.duration);
    this.timers.set(toast.id, timer);
  }
}
