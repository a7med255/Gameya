import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-notification-center',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="toast-region" aria-label="الإشعارات" aria-live="polite">
      @for (item of notifications.notifications(); track item.id) {
        <div class="toast toast--{{ item.kind }}" role="status">
          <span>{{ item.message }}</span>
          <button type="button" (click)="notifications.dismiss(item.id)" aria-label="إغلاق الإشعار">×</button>
        </div>
      }
    </section>
  `,
})
export class NotificationCenterComponent {
  protected readonly notifications = inject(NotificationService);
}
