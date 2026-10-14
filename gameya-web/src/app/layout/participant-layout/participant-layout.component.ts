import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';

@Component({
  selector: 'app-participant-layout',
  standalone: true,
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="participant-header">
      <div class="brand-mark"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
      <button class="btn btn--ghost" type="button" (click)="auth.logout()">تسجيل الخروج</button>
    </header>
    <main id="main-content"><router-outlet /></main>
  `,
  styles: [`
    .participant-header { min-height: 4.5rem; display: flex; align-items: center; justify-content: space-between; padding-inline: clamp(1rem, 5vw, 4rem); border-block-end: 1px solid var(--line); background: #fff; }
  `],
})
export class ParticipantLayoutComponent { protected readonly auth = inject(AuthService); }
