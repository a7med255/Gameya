import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { PAYMENT_METHOD_LABELS } from '../../core/constants/app.constants';
import { PagedResult, PaymentDto } from '../../core/models/api.models';
import { PaymentsApiService } from '../../core/services/payments-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-payments-history', standalone: true, imports: [MoneyPipe, ArabicDatePipe, EmptyStateComponent, LoadingStateComponent, StatusBadgeComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page"><header class="page-header"><div><h1 class="page-title">المدفوعات</h1><p class="page-subtitle">سجل المدفوعات والتوزيعات المعتمدة.</p></div></header>
      @if (loading()) { <app-loading-state /> } @else if (error()) { <app-empty-state icon="!" title="تعذر تحميل المدفوعات" message="حاول تاني بعد شوية." actionLabel="إعادة المحاولة" (action)="load(1)" /> } @else if (result().items.length === 0) { <app-empty-state icon="م" title="مفيش مدفوعات مسجلة حتى الآن" message="أول دفعة تسجلها هتظهر هنا بتوزيعها الكامل." /> } @else {
        <div class="table-wrap desktop-table"><table class="data-table"><thead><tr><th>المبلغ</th><th>الطريقة</th><th>التاريخ والوقت</th><th>النوع</th><th>التوزيعات</th><th>التأخير</th></tr></thead><tbody>@for (payment of result().items; track payment.id) { <tr><td><strong class="money">{{ payment.amount | money }}</strong></td><td>{{ methodLabels[payment.method] }}</td><td>{{ payment.paidAtUtc | arabicDate:true }}</td><td><app-status-badge [status]="payment.entryType === 'Payment' ? 'Paid' : 'Pending'" /></td><td>{{ payment.allocations.length }} فترة@if (payment.credits.length) { · {{ payment.credits.length }} رصيد }</td><td>{{ maxLateDays(payment) ? maxLateDays(payment) + ' أيام' : 'في الموعد' }}</td></tr> }</tbody></table></div>
        <div class="mobile-list">@for (payment of result().items; track payment.id) { <article class="mobile-card"><div class="split"><strong class="money">{{ payment.amount | money }}</strong><app-status-badge [status]="payment.entryType === 'Payment' ? 'Paid' : 'Pending'" /></div><dl class="detail-list" style="margin-top:.75rem"><div><dt>الطريقة</dt><dd>{{ methodLabels[payment.method] }}</dd></div><div><dt>التاريخ</dt><dd>{{ payment.paidAtUtc | arabicDate:true }}</dd></div><div><dt>الفترات</dt><dd>{{ payment.allocations.length }}</dd></div></dl></article> }</div>
        <div class="pagination"><button class="btn btn--secondary" type="button" [disabled]="result().page <= 1" (click)="load(result().page - 1)">السابق</button><span>صفحة {{ result().page }} من {{ totalPages() }}</span><button class="btn btn--secondary" type="button" [disabled]="result().page >= totalPages()" (click)="load(result().page + 1)">التالي</button></div>
      }
    </div>
  `,
})
export class PaymentsHistoryComponent {
  private readonly api = inject(PaymentsApiService); protected readonly result = signal<PagedResult<PaymentDto>>({ items: [], page: 1, pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false }); protected readonly loading = signal(true); protected readonly error = signal(false); protected readonly methodLabels = PAYMENT_METHOD_LABELS;
  constructor() { this.load(1); }
  protected load(page: number): void { this.loading.set(true); this.error.set(false); this.api.list(null, page).subscribe({ next: (result) => { this.result.set(result); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
  protected totalPages(): number { return Math.max(1, Math.ceil(this.result().totalCount / this.result().pageSize)); }
  protected maxLateDays(payment: PaymentDto): number { return Math.max(0, ...payment.allocations.map((x) => x.lateDays)); }
}
