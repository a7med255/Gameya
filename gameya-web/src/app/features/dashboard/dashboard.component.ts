import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DashboardDto } from '../../core/models/api.models';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { safePercent } from '../../shared/utils/display.utils';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';

@Component({
  selector: 'app-dashboard', standalone: true, imports: [RouterLink, MoneyPipe, EmptyStateComponent, LoadingStateComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><div><h1 class="page-title">الرئيسية</h1><p class="page-subtitle">ملخص سريع لكل جمعياتك وأهم الأرقام.</p></div><a class="btn btn--primary" routerLink="/owner/payments/new">+ تسجيل دفعة</a></header>
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="مقدرناش نحمّل الرئيسية" message="حاول تاني، ولو المشكلة مستمرة راجع اتصال الخدمة." actionLabel="إعادة المحاولة" (action)="load()" /> }
      @else if (dashboard(); as data) {
        <section class="stat-grid" aria-label="ملخص الأرقام">
          <article class="card card--brand stat"><span class="stat__label">إجمالي المطلوب</span><strong class="stat__value money">{{ data.totalDue | money }}</strong></article>
          <article class="card stat"><span class="stat__label">إجمالي المدفوع</span><strong class="stat__value success-text money">{{ data.totalPaid | money }}</strong></article>
          <article class="card stat"><span class="stat__label">المتبقي</span><strong class="stat__value money">{{ data.totalRemaining | money }}</strong></article>
          <article class="card stat"><span class="stat__label">المتأخرات</span><strong class="stat__value" [class.danger-text]="data.outstandingPayments > 0">{{ data.outstandingPayments }}</strong></article>
        </section>
        <section class="content-grid" style="margin-top:1rem">
          <article class="card">
            <div class="split"><div><h2 class="section-title">تقدم التحصيل</h2><p class="muted">نسبة المبالغ المسجلة من إجمالي المطلوب.</p></div><strong>{{ progress() }}٪</strong></div>
            <div class="progress" role="progressbar" aria-label="نسبة التحصيل" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="progress()"><div class="progress__bar" [style.width.%]="progress()"></div></div>
            <div class="stat-grid compact-stats">
              <div><span class="muted">جمعيات نشطة</span><strong>{{ data.activeAssociations }}</strong></div>
              <div><span class="muted">الأشخاص</span><strong>{{ data.totalPeople }}</strong></div>
              <div><span class="muted">قبض الشهر</span><strong>{{ data.currentPayouts }}</strong></div>
            </div>
          </article>
          <aside class="card">
            <h2 class="section-title">إجراءات سريعة</h2>
            <div class="stack"><a class="btn btn--secondary btn--block" routerLink="/owner/people">إضافة شخص</a><a class="btn btn--secondary btn--block" routerLink="/owner/associations/new">إنشاء جمعية</a><a class="btn btn--secondary btn--block" routerLink="/owner/reports">تقرير الشهر</a><a class="btn btn--primary btn--block" routerLink="/owner/reports">تقارير الجمعيات</a></div>
          </aside>
        </section>
      }
    </div>
  `,
  styles: [`.compact-stats { margin-top:1.5rem; grid-template-columns:repeat(3,1fr); }.compact-stats > div { display:grid; gap:.3rem; padding:1rem; background:var(--canvas); border-radius:.8rem; }.compact-stats strong { font-size:1.45rem; } @media(max-width:600px){.compact-stats{grid-template-columns:1fr;}}`],
})
export class DashboardComponent {
  private readonly api = inject(DashboardApiService);
  protected readonly loading = signal(true); protected readonly error = signal(false); protected readonly dashboard = signal<DashboardDto | null>(null);
  protected readonly progress = computed(() => { const value = this.dashboard(); return value ? safePercent(value.totalPaid, value.totalDue) : 0; });
  constructor() { this.load(); }
  protected load(): void { this.loading.set(true); this.error.set(false); this.api.get().subscribe({ next: (data) => { this.dashboard.set(data); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
}
