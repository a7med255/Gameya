import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, EMPTY, startWith, switchMap, tap } from 'rxjs';
import { ASSOCIATION_STATUS_LABELS } from '../../core/constants/app.constants';
import { AssociationStatus, AssociationSummaryDto, PagedResult } from '../../core/models/api.models';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-associations-list', standalone: true, imports: [ReactiveFormsModule, RouterLink, MoneyPipe, ArabicDatePipe, EmptyStateComponent, LoadingStateComponent, StatusBadgeComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><div><h1 class="page-title">الجمعيات</h1><p class="page-subtitle">تابع كل جمعية من المسودة لحد الاكتمال.</p></div><a class="btn btn--primary" routerLink="/owner/associations/new">+ جمعية جديدة</a></header>
      <section class="card card--flat filters"><div class="search-box"><span class="search-box__icon">⌕</span><label class="sr-only" for="association-search">البحث</label><input class="control" id="association-search" type="search" placeholder="ابحث باسم الجمعية…" [formControl]="search"></div><div class="field"><label class="sr-only" for="status">الحالة</label><select class="control" id="status" [formControl]="status"><option value="">كل الحالات</option>@for (entry of statuses; track entry.value) { <option [value]="entry.value">{{ entry.label }}</option> }</select></div></section>
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="تعذر تحميل الجمعيات" message="حاول تاني بعد شوية." actionLabel="إعادة المحاولة" (action)="loadPage(1)" /> }
      @else if (result().items.length === 0) { <app-empty-state icon="ج" title="لسه مفيش جمعيات مضافة" message="أنشئ أول جمعية وحدد الأعضاء وجدول القبض." actionLabel="إنشاء جمعية" (action)="goToCreate.click()" /><a #goToCreate hidden routerLink="/owner/associations/new"></a> }
      @else {
        <div class="association-grid">@for (association of result().items; track association.id) { <a class="card association-card" [routerLink]="['/owner/associations', association.id]"><div class="split"><div class="association-icon">ج</div><app-status-badge [status]="association.status" /></div><h2>{{ association.name }}</h2><dl class="detail-list"><div><dt>الاشتراك الشهري</dt><dd class="money">{{ association.monthlyContribution | money }}</dd></div><div><dt>المدة</dt><dd>{{ association.numberOfMonths }} شهر</dd></div><div><dt>الأعضاء</dt><dd>{{ association.memberCount }}</dd></div><div><dt>البداية</dt><dd>{{ association.startDate | arabicDate }}</dd></div></dl></a> }</div>
        <div class="pagination"><button class="btn btn--secondary" type="button" [disabled]="result().page <= 1" (click)="loadPage(result().page - 1)">السابق</button><span>صفحة {{ result().page }} من {{ totalPages() }}</span><button class="btn btn--secondary" type="button" [disabled]="result().page >= totalPages()" (click)="loadPage(result().page + 1)">التالي</button></div>
      }
    </div>
  `,
  styles: [`.filters{display:flex;gap:1rem;margin-bottom:1rem}.filters .field{min-width:12rem}.association-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:1rem}.association-card{transition:.18s ease}.association-card:hover{transform:translateY(-2px);border-color:var(--brand-600);box-shadow:var(--shadow-lg)}.association-card h2{margin:1rem 0}.association-icon{width:2.7rem;height:2.7rem;display:grid;place-items:center;border-radius:.8rem;background:var(--brand-100);color:var(--brand-900);font-weight:900}@media(max-width:1100px){.association-grid{grid-template-columns:repeat(2,1fr)}}@media(max-width:767px){.filters{display:grid}.filters .field{min-width:0}.association-grid{grid-template-columns:1fr}}`],
})
export class AssociationsListComponent {
  private readonly api = inject(AssociationsApiService);
  protected readonly search = new FormControl('', { nonNullable: true }); protected readonly status = new FormControl<AssociationStatus | ''>('', { nonNullable: true });
  protected readonly result = signal<PagedResult<AssociationSummaryDto>>({ items: [], page: 1, pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false }); protected readonly loading = signal(true); protected readonly error = signal(false);
  protected readonly statuses = Object.entries(ASSOCIATION_STATUS_LABELS).map(([value, label]) => ({ value: value as AssociationStatus, label }));
  constructor() { this.search.valueChanges.pipe(startWith(''), debounceTime(350), distinctUntilChanged(), switchMap(() => this.query(1))).subscribe(); this.status.valueChanges.pipe(distinctUntilChanged()).subscribe(() => this.loadPage(1)); }
  protected totalPages(): number { return Math.max(1, Math.ceil(this.result().totalCount / this.result().pageSize)); }
  protected loadPage(page: number): void { this.query(page).subscribe(); }
  private query(page: number) {
    this.loading.set(true); this.error.set(false);
    return this.api.list(this.search.value, this.status.value, page).pipe(
      tap((result) => { this.result.set(result); this.loading.set(false); }),
      catchError(() => { this.error.set(true); this.loading.set(false); return EMPTY; }),
    );
  }
}
