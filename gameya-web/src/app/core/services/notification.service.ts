import { Injectable, signal } from '@angular/core';

export type NotificationKind = 'success' | 'error' | 'warning' | 'info';
export interface AppNotification { id: number; kind: NotificationKind; message: string; }

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private nextId = 1;
  private readonly state = signal<readonly AppNotification[]>([]);
  readonly notifications = this.state.asReadonly();

  success(message: string): void { this.show('success', message); }
  error(message: string): void { this.show('error', message, 7000); }
  warning(message: string): void { this.show('warning', message, 6000); }
  info(message: string): void { this.show('info', message); }

  dismiss(id: number): void { this.state.update((items) => items.filter((item) => item.id !== id)); }

  private show(kind: NotificationKind, message: string, timeout = 4500): void {
    const item = { id: this.nextId++, kind, message } as const;
    this.state.update((items) => [...items, item]);
    window.setTimeout(() => this.dismiss(item.id), timeout);
  }
}
