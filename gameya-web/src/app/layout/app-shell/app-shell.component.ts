import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { TranslationKey } from '../../core/services/localization.service';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

interface NavItem { label: TranslationKey; path: string; icon: string; }

const OWNER_NAV: readonly NavItem[] = [
  { label: 'nav.dashboard', path: '/owner/dashboard', icon: '⌂' },
  { label: 'nav.people', path: '/owner/people', icon: 'أ' },
  { label: 'nav.associations', path: '/owner/associations', icon: 'ج' },
  { label: 'nav.recordPayment', path: '/owner/payments/new', icon: 'د' },
  { label: 'nav.payments', path: '/owner/payments', icon: 'م' },
  { label: 'nav.replacements', path: '/owner/replacements', icon: 'ب' },
  { label: 'nav.settlements', path: '/owner/settlements', icon: 'ت' },
  { label: 'nav.reports', path: '/owner/reports', icon: 'ق' },
  { label: 'nav.settings', path: '/owner/settings', icon: '⚙' },
];

const PAYMENT_NAV: readonly NavItem[] = [
  { label: 'nav.recordPayment', path: '/payment/pay', icon: 'د' },
  { label: 'nav.payments', path: '/payment/history', icon: 'م' },
];

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="shell" [class.shell--drawer]="drawerOpen()">
      <button class="drawer-backdrop" type="button" aria-label="إغلاق القائمة" (click)="drawerOpen.set(false)"></button>
      <aside class="sidebar" aria-label="القائمة الرئيسية">
        <a class="sidebar__brand" routerLink="/">
          <span class="brand-mark__logo">ج</span>
          <span>جمعية</span>
        </a>
        <nav class="sidebar__nav">
          @for (item of navigation(); track item.path) {
            <a [routerLink]="item.path" routerLinkActive="nav-link--active" class="nav-link" (click)="drawerOpen.set(false)">
              <span class="nav-link__icon" aria-hidden="true">{{ item.icon }}</span>
              <span>{{ item.label | translate }}</span>
            </a>
          }
        </nav>
        <div class="sidebar__footer">
          <span class="muted">{{ roleLabel() }}</span>
          <button class="btn btn--ghost btn--block" type="button" (click)="auth.logout()">تسجيل الخروج</button>
        </div>
      </aside>

      <div class="shell__body">
        <header class="topbar">
          <button class="icon-btn menu-button" type="button" aria-label="فتح القائمة" [attr.aria-expanded]="drawerOpen()" (click)="drawerOpen.set(true)">☰</button>
          <a class="mobile-brand" routerLink="/">جمعية</a>
          <div class="topbar__user">
            <span class="avatar" aria-hidden="true">{{ initial() }}</span>
            <span class="topbar__name">{{ auth.currentUser()?.displayName }}</span>
          </div>
        </header>
        <main id="main-content" tabindex="-1"><router-outlet /></main>
      </div>

      <nav class="bottom-nav" aria-label="تنقل الموبايل">
        @for (item of mobileNavigation(); track item.path) {
          <a [routerLink]="item.path" routerLinkActive="bottom-nav__item--active" class="bottom-nav__item">
            <span aria-hidden="true">{{ item.icon }}</span><span>{{ item.label | translate }}</span>
          </a>
        }
        <button class="bottom-nav__item" type="button" (click)="drawerOpen.set(true)"><span>☰</span><span>المزيد</span></button>
      </nav>
    </div>
  `,
  styles: [`
    .shell { min-height: 100dvh; }
    .sidebar { position: fixed; z-index: 50; inset-block: 0; inset-inline-start: 0; width: var(--sidebar-width); display: flex; flex-direction: column; padding: 1rem; background: var(--brand-950); color: #fff; }
    .sidebar__brand { display: flex; align-items: center; gap: .75rem; padding: .5rem; font-size: 1.45rem; font-weight: 900; }
    .sidebar__brand .brand-mark__logo { background: #fff; color: var(--brand-900); }
    .sidebar__nav { flex: 1; display: grid; align-content: start; gap: .25rem; margin-block-start: 1.5rem; overflow-y: auto; }
    .nav-link { display: flex; align-items: center; gap: .8rem; min-height: 2.9rem; padding: .6rem .75rem; color: rgb(255 255 255 / 72%); border-radius: .7rem; font-weight: 700; }
    .nav-link:hover, .nav-link--active { color: #fff; background: rgb(255 255 255 / 11%); }
    .nav-link__icon { width: 1.8rem; height: 1.8rem; display: grid; place-items: center; border-radius: .5rem; background: rgb(255 255 255 / 9%); }
    .sidebar__footer { border-block-start: 1px solid rgb(255 255 255 / 12%); padding-block-start: 1rem; }
    .sidebar__footer .muted { display: block; padding-inline: .75rem; color: rgb(255 255 255 / 58%); font-size: .8rem; }
    .sidebar__footer .btn { color: #fff; margin-block-start: .35rem; }
    .shell__body { margin-inline-start: var(--sidebar-width); min-height: 100dvh; }
    .topbar { height: 4.5rem; padding-inline: 1.5rem; position: sticky; z-index: 40; top: 0; display: flex; align-items: center; justify-content: flex-end; border-block-end: 1px solid var(--line); background: rgb(255 255 255 / 88%); backdrop-filter: blur(14px); }
    .topbar__user { display: flex; align-items: center; gap: .65rem; font-weight: 700; }
    .avatar { width: 2.35rem; height: 2.35rem; display: grid; place-items: center; border-radius: 50%; color: var(--brand-900); background: var(--brand-100); font-weight: 900; }
    .menu-button, .mobile-brand, .bottom-nav, .drawer-backdrop { display: none; }
    @media (max-width: 1023px) {
      .sidebar { width:min(var(--sidebar-width),calc(100vw - 1rem));transform: translateX(105%); box-shadow: var(--shadow-lg); transition: transform .2s ease; overscroll-behavior:contain; }
      :host-context([dir='ltr']) .sidebar { transform: translateX(-105%); }
      .shell--drawer .sidebar { transform: translateX(0); }
      .drawer-backdrop { position: fixed; z-index: 45; inset: 0; border: 0; background: rgb(7 59 58 / 48%); }
      .shell--drawer .drawer-backdrop { display: block; }
      .shell__body { margin-inline-start: 0; }
      .topbar { justify-content: space-between; padding-inline: 1rem; }
      .menu-button, .mobile-brand { display: inline-grid; }
      .mobile-brand { font-size: 1.25rem; color: var(--brand-800); font-weight: 900; }
    }
    @media (max-width: 767px) {
      .topbar { height: 4rem; }
      .topbar__name { display: none; }
      .bottom-nav { position: fixed; z-index: 35; inset-inline: .75rem; inset-block-end: max(.65rem,env(safe-area-inset-bottom)); min-height: 4.4rem; display: grid; grid-template-columns: repeat(4, 1fr); align-items: center; border: 1px solid var(--line); border-radius: 1.25rem; background: rgb(255 255 255 / 94%); box-shadow: var(--shadow-lg); backdrop-filter: blur(14px); overflow: hidden; }
      .bottom-nav__item { min-width: 0; min-height: 4rem; border: 0; background: transparent; display: grid; place-content: center; gap: .1rem; text-align: center; color: var(--ink-500); font-size: .7rem; font-weight: 700; }
      .bottom-nav__item > span:first-child { font-size: 1rem; }
      .bottom-nav__item--active { color: var(--brand-800); background: var(--brand-50); }
    }
  `],
})
export class AppShellComponent {
  protected readonly auth = inject(AuthService);
  protected readonly drawerOpen = signal(false);
  protected readonly navigation = computed(() => this.auth.hasRole('PaymentUser') ? PAYMENT_NAV : OWNER_NAV);
  protected readonly mobileNavigation = computed(() => this.navigation().slice(0, 3));
  protected readonly initial = computed(() => this.auth.currentUser()?.displayName.trim().charAt(0) || 'ج');
  protected readonly roleLabel = computed(() => this.auth.hasRole('Admin') ? 'مدير المنصة' : this.auth.hasRole('Owner') ? 'مالك الجمعيات' : 'مسجل المدفوعات');
}
