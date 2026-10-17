import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';
import { MonthlyCollectionPersonDto, MonthlyPayoutPersonDto, PagedResult } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { DashboardApiService } from '../../core/services/dashboard-api.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReportsApiService } from '../../core/services/reports-api.service';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { egyptCurrentMonth } from '../../shared/utils/display.utils';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';

@Component({
  selector: 'app-monthly-finance', standalone: true,
  imports: [ReactiveFormsModule, MoneyPipe, ArabicDatePipe, EmptyStateComponent, LoadingStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page monthly-page">
      <header class="page-header"><div><h1 class="page-title">{{ isPayouts ? 'مين هيقبض الشهر ده؟' : 'كشف التحصيل الشهري' }}</h1><p class="page-subtitle">{{ isPayouts ? 'إجمالي القبض بعد خصم اشتراكات نفس الشهر في كل الجمعيات.' : 'كل شخص مرة واحدة مع تفاصيل التزاماته في الجمعيات.' }}</p></div></header>
      <form class="card month-filter" [formGroup]="filter">
        <div class="field"><label for="finance-month">الشهر</label><select id="finance-month" class="control" formControlName="month">@for (item of months; track item.value) { <option [value]="item.value">{{ item.label }}</option> }</select></div>
        <div class="field"><label for="finance-year">السنة</label><input id="finance-year" class="control" type="number" min="2000" max="2200" formControlName="year"></div>
        @if (!isPayouts) { <label class="check"><input type="checkbox" formControlName="overdueOnly"> المتأخرات فقط</label> }
        <button type="button" class="btn btn--primary" (click)="apply()">عرض</button>
      </form>
      @if (!isPayouts) { <div class="report-actions"><button class="btn btn--secondary" type="button" [disabled]="busy()" (click)="download()">تنزيل PDF</button><button class="btn btn--secondary" type="button" [disabled]="busy()" (click)="send()">إرسال بالبريد</button></div> }
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="تعذر تحميل البيانات" message="حاول مرة أخرى." actionLabel="إعادة المحاولة" (action)="load()" /> }
      @else if (isPayouts) {
        @if (payouts().items.length === 0) { <app-empty-state icon="✓" title="لا يوجد قبض في هذا الشهر" message="لا توجد مستحقات قبض مسجلة للفترة المختارة." /> }
        <div class="person-list">@for (person of payouts().items; track person.personId) {
          <article class="card person-card"><div class="person-head"><div><h2>{{ person.personName }}</h2><p class="muted">{{ person.payouts.length }} مستحقات قبض</p></div><div class="net"><small>صافي المبلغ الذي سيستلمه</small><strong>{{ person.netPayout | money }}</strong>@if(person.amountOwed > 0){<span class="danger-text">المطلوب منه: {{ person.amountOwed | money }}</span>}</div></div>
            <div class="totals"><div><span>إجمالي القبض</span><strong>{{ person.scheduledPayouts | money }}</strong></div><div><span>اشتراكات الشهر بكل الجمعيات</span><strong>{{ person.contributionsDue | money }}</strong></div><div><span>المتأخر من التزامات الشهر</span><strong>{{ person.historicalOverdue | money }}</strong></div></div>
            <details><summary>تفاصيل الحساب</summary><div class="details-grid"><section><h3>القبض</h3>@for(item of person.payouts;track item.associationId + item.periodId){<p><span>{{item.associationName}} · الدور {{item.periodNumber}}</span><strong>{{item.amount|money}}</strong></p>}</section><section><h3>اشتراكات الشهر</h3>@for(item of person.contributions;track item.associationId + item.periodId){<p><span>{{item.associationName}}</span><strong>{{item.required|money}}</strong></p>}</section></div></details>
          </article> }
        </div>
      } @else {
        @if (collections().items.length === 0) { <app-empty-state icon="✓" title="لا توجد التزامات" message="لا توجد مبالغ تحصيل للفترة المختارة." /> }
        <div class="person-list">@for (person of collections().items; track person.personId) {
          <article class="card person-card"><div class="person-head"><div><h2>{{ person.personName }}</h2><p class="muted">{{ person.associations.length }} جمعيات</p></div><div class="net"><small>{{ person.scheduledPayouts > 0 ? 'صافي القبض' : 'المتبقي تحصيله' }}</small><strong>{{ (person.scheduledPayouts > 0 ? person.netPayout : person.remaining) | money }}</strong></div></div>
            <div class="totals"><div><span>المطلوب</span><strong>{{person.required|money}}</strong></div><div><span>المدفوع</span><strong>{{person.paid|money}}</strong></div><div><span>المتأخر</span><strong [class.danger-text]="person.overdue > 0">{{person.overdue|money}}</strong></div></div>
            <details><summary>تفاصيل الجمعيات</summary>@for(item of person.associations;track item.associationId + item.periodId){<p class="detail-row"><span>{{item.associationName}} · استحقاق {{item.dueDate|arabicDate}}</span><strong>{{item.required|money}}</strong></p>}</details>
          </article> }
        </div>
      }
      @if (result().totalPages > 1) { <nav class="pagination"><button class="btn btn--secondary" type="button" [disabled]="!result().hasPrevious" (click)="go(page()-1)">السابق</button><span>صفحة {{result().page}} من {{result().totalPages}}</span><button class="btn btn--secondary" type="button" [disabled]="!result().hasNext" (click)="go(page()+1)">التالي</button></nav> }
    </div>`,
  styles: [`
    .monthly-page{max-width:1100px}.month-filter{display:flex;align-items:end;gap:1rem;flex-wrap:wrap}.month-filter .field{min-width:10rem}.check{display:flex;align-items:center;gap:.5rem;min-height:3rem}.report-actions{display:flex;gap:.75rem;margin-block:1rem}.person-list{display:grid;gap:1rem;margin-top:1rem}.person-head{display:flex;justify-content:space-between;gap:1rem}.person-head h2{margin:0}.net{display:grid;text-align:left}.net strong{font-size:1.35rem;color:var(--brand-700)}.totals{display:grid;grid-template-columns:repeat(3,1fr);gap:.75rem;margin-block:1rem}.totals>div{display:grid;gap:.25rem;padding:.75rem;background:var(--canvas);border-radius:.7rem}.totals span,.net small{color:var(--ink-500)}summary{cursor:pointer;font-weight:700}.details-grid{display:grid;grid-template-columns:1fr 1fr;gap:1rem}.details-grid p,.detail-row{display:flex;justify-content:space-between;gap:1rem;border-bottom:1px solid var(--line);padding-block:.5rem}.pagination{display:flex;align-items:center;justify-content:center;gap:1rem;margin-top:1rem}
    @media(max-width:600px){.month-filter{display:grid;grid-template-columns:1fr 1fr}.month-filter .field{min-width:0}.month-filter .btn,.check{grid-column:1/-1}.person-head{display:grid}.net{text-align:right}.totals,.details-grid{grid-template-columns:1fr}.report-actions .btn{flex:1}}
  `],
})
export class MonthlyFinanceComponent {
  private readonly api = inject(DashboardApiService); private readonly reports = inject(ReportsApiService);
  private readonly route = inject(ActivatedRoute); private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService); private readonly errors = inject(ApiErrorService); private readonly confirm = inject(ConfirmService);
  protected readonly isPayouts = this.route.snapshot.data['mode'] === 'payouts';
  private readonly current = egyptCurrentMonth();
  protected readonly loading = signal(true); protected readonly busy = signal(false); protected readonly error = signal(false); protected readonly page = signal(1);
  protected readonly collections = signal<PagedResult<MonthlyCollectionPersonDto>>(this.empty());
  protected readonly payouts = signal<PagedResult<MonthlyPayoutPersonDto>>(this.empty());
  protected readonly months = Array.from({ length: 12 }, (_, index) => ({ value: index + 1, label: new Intl.DateTimeFormat('ar-EG', { month: 'long' }).format(new Date(2026, index, 1)) }));
  protected readonly filter = new FormGroup({
    year: new FormControl(Number(this.route.snapshot.queryParamMap.get('year')) || this.current.year, { nonNullable: true }),
    month: new FormControl(Number(this.route.snapshot.queryParamMap.get('month')) || this.current.month, { nonNullable: true }),
    overdueOnly: new FormControl(this.route.snapshot.queryParamMap.get('view') === 'overdue', { nonNullable: true }),
  });
  constructor() { this.load(); }
  protected result(): PagedResult<unknown> { return this.isPayouts ? this.payouts() : this.collections(); }
  protected apply(): void { this.page.set(1); this.updateUrl(); this.load(); }
  protected go(page: number): void { this.page.set(page); this.load(); }
  protected load(): void {
    const { year, month, overdueOnly } = this.filter.getRawValue(); this.loading.set(true); this.error.set(false);
    const failed = () => { this.error.set(true); this.loading.set(false); };
    if (this.isPayouts) this.api.payouts(year, month, this.page()).subscribe({ next: (data) => { this.payouts.set(data); this.loading.set(false); }, error: failed });
    else this.api.collections(year, month, overdueOnly, this.page()).subscribe({ next: (data) => { this.collections.set(data); this.loading.set(false); }, error: failed });
  }
  protected download(): void { const {year,month}=this.filter.getRawValue(); this.busy.set(true); this.reports.download(year,month).pipe(finalize(()=>this.busy.set(false))).subscribe({next:(blob)=>{const url=URL.createObjectURL(blob);const anchor=document.createElement('a');anchor.href=url;anchor.download=`كشف-التحصيل-${year}-${String(month).padStart(2,'0')}.pdf`;anchor.click();URL.revokeObjectURL(url);},error:(error:unknown)=>this.notifications.error(this.errors.message(error))}); }
  protected async send(): Promise<void> { const {year,month}=this.filter.getRawValue(); if(!await this.confirm.ask({title:'إرسال كشف التحصيل',message:'سيتم إرسال كشف الشهر المختار إلى بريدك المسجل.',confirmLabel:'إرسال'}))return;this.busy.set(true);this.reports.send(year,month).pipe(finalize(()=>this.busy.set(false))).subscribe({next:()=>this.notifications.success('تم إرسال كشف التحصيل إلى بريدك.'),error:(error:unknown)=>this.notifications.error(this.errors.message(error))}); }
  private updateUrl(): void { const value=this.filter.getRawValue(); void this.router.navigate([], {relativeTo:this.route,queryParams:{year:value.year,month:value.month,view:value.overdueOnly?'overdue':null},replaceUrl:true}); }
  private empty<T>(): PagedResult<T> { return {items:[],page:1,pageNumber:1,pageSize:20,totalCount:0,totalPages:0,hasNext:false,hasPrevious:false}; }
}
