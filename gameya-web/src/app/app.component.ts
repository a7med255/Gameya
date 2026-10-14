import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmDialogComponent } from './shared/ui/confirm-dialog.component';
import { NotificationCenterComponent } from './shared/ui/notification-center.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, NotificationCenterComponent, ConfirmDialogComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <a class="skip-link" href="#main-content">انتقل للمحتوى الرئيسي</a>
    <router-outlet />
    <app-notification-center />
    <app-confirm-dialog />
  `,
})
export class AppComponent {}
