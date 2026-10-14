import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { PersonProfileDto } from '../../core/models/api.models';
import { PeopleApiService } from '../../core/services/people-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-person-profile', standalone: true, imports: [RouterLink, ArabicDatePipe, MoneyPipe, EmptyStateComponent, LoadingStateComponent, StatusBadgeComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a class="btn btn--ghost" routerLink="/owner/people">→ رجوع للأشخاص</a>
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="الملف غير متاح" message="الشخص غير موجود أو مش متاح لحسابك." /> }
      @else if (profile(); as data) {
        <header class="page-header"><div><h1 class="page-title">{{ data.person.fullName }}</h1><p class="page-subtitle"><span dir="ltr">{{ data.person.phoneNumber }}</span> · كود <span class="code">{{ data.person.subscriberCode }}</span></p></div><a class="btn btn--primary" [routerLink]="['/owner/payments/new']" [queryParams]="{ personId: data.person.id }">تسجيل دفعة</a></header>
        <section class="stat-grid"><article class="card stat"><span class="stat__label">إجمالي المطلوب</span><strong class="stat__value money">{{ data.totalObligations | money }}</strong></article><article class="card stat"><span class="stat__label">المدفوع</span><strong class="stat__value success-text money">{{ data.totalPaid | money }}</strong></article><article class="card stat"><span class="stat__label">المتبقي</span><strong class="stat__value money">{{ data.totalRemaining | money }}</strong></article><article class="card stat"><span class="stat__label">عدد الجمعيات</span><strong class="stat__value">{{ data.memberships.length }}</strong></article></section>
        <section class="card" style="margin-top:1rem"><h2 class="section-title">الجمعيات والحصص</h2>@if (data.memberships.length === 0) { <p class="muted">مشتركش في أي جمعية لسه.</p> } @else { <div class="table-wrap"><table class="data-table"><thead><tr><th>الجمعية</th><th>النوع</th><th>الحصص</th><th>المدفوع</th><th>المتبقي</th><th>الحالة</th></tr></thead><tbody>@for (membership of data.memberships; track membership.membershipId) { <tr><td><a [routerLink]="['/owner/associations', membership.associationId]"><strong>{{ membership.associationName }}</strong></a></td><td>{{ membership.kind === 'Advance' ? 'سلف' : 'عضو دافع' }}</td><td>{{ membership.shareCount }}</td><td class="money">{{ membership.paid | money }}</td><td class="money">{{ membership.remaining | money }}</td><td><app-status-badge [status]="membership.status" /></td></tr> }</tbody></table></div> }</section>
        <section class="content-grid" style="margin-top:1rem">
          <article class="card"><h2 class="section-title">سجل المدفوعات</h2>@if (data.payments.length === 0) { <p class="muted">مفيش مدفوعات مسجلة حتى الآن.</p> } @else { <div class="stack">@for (payment of data.payments; track payment.id) { <div class="split"><div><strong class="money">{{ payment.amount | money }}</strong><div class="muted">{{ payment.paidAtUtc | arabicDate:true }}</div></div><app-status-badge [status]="payment.entryType === 'Payment' ? 'Paid' : 'Pending'" /></div> }</div> }</article>
          <article class="card"><h2 class="section-title">التسويات</h2>@if (data.settlements.length === 0) { <p class="muted">مفيش تسويات مسجلة.</p> } @else { <div class="stack">@for (settlement of data.settlements; track settlement.id) { <div><div class="split"><span>الرصيد</span><strong class="money">{{ settlement.balance | money }}</strong></div><app-status-badge [status]="settlement.status" /></div> }</div> }</article>
        </section>
      }
    </div>
  `,
})
export class PersonProfileComponent {
  private readonly api = inject(PeopleApiService); private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? '';
  protected readonly loading = signal(true); protected readonly error = signal(false); protected readonly profile = signal<PersonProfileDto | null>(null);
  constructor() { this.api.profile(this.id).subscribe({ next: (data) => { this.profile.set(data); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
}
