import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DashboardDto } from '../../core/models/api.models';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { egyptCurrentMonth } from '../../shared/utils/display.utils';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';

@Component({
  selector: 'app-dashboard', standalone: true,
  imports: [RouterLink, ReactiveFormsModule, DecimalPipe, MoneyPipe, EmptyStateComponent, LoadingStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header">
        <div><h1 class="page-title">الرئيسية</h1><p class="page-subtitle">ملخص مالي دقيق للشهر المختار.</p></div>
        <a class="btn btn--primary" routerLink="/owner/payments/new">+ تسجيل دفعة</a>
      </header>
      <form class="card month-filter" [formGroup]="filter" aria-label="اختيار شهر التقرير">
        <div class="field"><label for="dashboard-month">الشهر</label><select id="dashboard-month" class="control" formControlName="month" (change)="reload()">@for (item of months; track item.value) { <option [value]="item.value">{{ item.label }}</option> }</select></div>
        <div class="field"><label for="dashboard-year">السنة</label><input id="dashboard-year" class="control" type="number" min="2000" max="2200" formControlName="year" (change)="reload()"></div>
        @if (dashboard(); as data) { <strong class="period-badge">البيانات المعروضة: {{ data.monthLabel }}</strong> }
      </form>
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="تعذر تحميل الرئيسية" message="حاول مرة أخرى." actionLabel="إعادة المحاولة" (action)="load()" /> }
      @else if (dashboard(); as data) {
        <section class="stat-grid" aria-label="ملخص الشهر">
          <article class="card card--brand stat"><span class="stat__label">إجمالي المطلوب</span><strong class="stat__value money">{{ data.totalDue | money }}</strong></article>
          <article class="card stat"><span class="stat__label">إجمالي المدفوع</span><strong class="stat__value success-text money">{{ data.totalPaid | money }}</strong></article>
          <article class="card stat"><span class="stat__label">المتبقي</span><strong class="stat__value money">{{ data.totalRemaining | money }}</strong></article>
          <a class="card stat stat-link" [routerLink]="['/owner/monthly-collection']" [queryParams]="{year:data.year,month:data.month,view:'overdue'}"><span class="stat__label">المتأخرات · {{ data.overduePeopleCount }} أشخاص</span><strong class="stat__value danger-text money">{{ data.totalOverdue | money }}</strong><small>عرض التفاصيل</small></a>
        </section>
        <section class="content-grid section-gap">
          <article class="card">
            <div class="split"><div><h2 class="section-title">تقدم التحصيل · {{ data.monthLabel }}</h2><p class="muted">نسبة المبالغ المخصصة لالتزامات هذا الشهر فقط.</p></div><strong>{{ data.collectionProgress | number:'1.0-1' }}٪</strong></div>
            <div class="progress" role="progressbar" aria-label="نسبة التحصيل" aria-valuemin="0" aria-valuemax="100" [attr.aria-valuenow]="data.collectionProgress"><div class="progress__bar" [style.width.%]="data.collectionProgress"></div></div>
            <div class="stat-grid compact-stats">
              <div><span class="muted">جمعيات نشطة</span><strong>{{ data.activeAssociations }}</strong></div>
              <div><span class="muted">الأشخاص</span><strong>{{ data.totalPeople }}</strong></div>
              <a [routerLink]="['/owner/monthly-payouts']" [queryParams]="{year:data.year,month:data.month}"><span class="muted">مين هيقبض الشهر ده؟</span><strong>{{ data.currentPayouts }}</strong></a>
            </div>
          </article>
          <aside class="card"><h2 class="section-title">إجراءات سريعة</h2><div class="stack">
            <a class="btn btn--secondary btn--block" routerLink="/owner/people">إضافة شخص</a>
            <a class="btn btn--secondary btn--block" routerLink="/owner/associations/new">إنشاء جمعية</a>
            <a class="btn btn--primary btn--block" [routerLink]="['/owner/monthly-collection']" [queryParams]="{year:data.year,month:data.month}">كشف التحصيل الشهري</a>
            <a class="btn btn--secondary btn--block" routerLink="/owner/reports">تقارير الجمعيات</a>
          </div></aside>
        </section>
      }
    </div>`,
  styles: [`
    .month-filter{display:flex;align-items:end;gap:1rem;margin-bottom:1rem;flex-wrap:wrap}.month-filter .field{min-width:10rem}.period-badge{margin-inline-start:auto;align-self:center;color:var(--brand-700)}
    .section-gap{margin-top:1rem}.stat-link{text-decoration:none;color:inherit}.stat-link small{color:var(--brand-700)}
    .compact-stats{margin-top:1.5rem;grid-template-columns:repeat(3,1fr)}.compact-stats>div,.compact-stats>a{display:grid;gap:.3rem;padding:1rem;background:var(--canvas);border-radius:.8rem;text-decoration:none;color:inherit}.compact-stats strong{font-size:1.45rem}
    @media(max-width:600px){.compact-stats{grid-template-columns:1fr}.month-filter{display:grid;grid-template-columns:1fr 1fr}.period-badge{grid-column:1/-1;margin:0}.month-filter .field{min-width:0}}
  `],
})
export class DashboardComponent {
  private readonly api = inject(DashboardApiService);
  private readonly current = egyptCurrentMonth();
  protected readonly loading = signal(true);
  protected readonly error = signal(false);
  protected readonly dashboard = signal<DashboardDto | null>(null);
  protected readonly months = Array.from({ length: 12 }, (_, index) => ({ value: index + 1, label: new Intl.DateTimeFormat('ar-EG', { month: 'long' }).format(new Date(2026, index, 1)) }));
  protected readonly filter = new FormGroup({
    year: new FormControl(this.current.year, { nonNullable: true }),
    month: new FormControl(this.current.month, { nonNullable: true }),
  });
  constructor() { this.load(); }
  protected reload(): void { if (this.filter.valid) this.load(); }
  protected load(): void {
    const { year, month } = this.filter.getRawValue();
    this.loading.set(true); this.error.set(false);
    this.api.get(year, month).subscribe({ next: (data) => { this.dashboard.set(data); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } });
  }
}
