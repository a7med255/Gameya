import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { PersonProfileDto } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-person-profile', standalone: true, imports: [RouterLink, ReactiveFormsModule, ArabicDatePipe, MoneyPipe, EmptyStateComponent, LoadingStateComponent, StatusBadgeComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <a class="btn btn--ghost" routerLink="/owner/people">→ رجوع للأشخاص</a>
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="الملف غير متاح" message="الشخص غير موجود أو مش متاح لحسابك." /> }
      @else if (profile(); as data) {
        <header class="page-header"><div><h1 class="page-title">{{ data.person.fullName }}</h1><p class="page-subtitle"><span dir="ltr">{{ data.person.phoneNumber }}</span> · كود <span class="code">{{ data.person.subscriberCode }}</span></p></div><div class="inline"><button class="btn btn--secondary" type="button" (click)="openEdit(data)">تعديل البيانات</button>@if (data.person.isActive) { <button class="btn btn--danger" type="button" (click)="deactivate()">إلغاء الشخص</button><a class="btn btn--primary" [routerLink]="['/owner/payments/new']" [queryParams]="{ personId: data.person.id }">تسجيل دفعة</a> }</div></header>
        @if (editing()) { <section class="card" style="margin-bottom:1rem"><div class="split"><h2 class="section-title">تعديل بيانات الشخص</h2><button class="icon-btn" type="button" aria-label="إغلاق" (click)="editing.set(false)">×</button></div><form class="form-grid" [formGroup]="editForm" (ngSubmit)="save()"><div class="field"><label for="editName">الاسم بالكامل</label><input class="control" id="editName" formControlName="fullName">@if (editForm.controls.fullName.invalid && editForm.controls.fullName.touched) { <span class="field__error">الاسم مطلوب.</span> }</div><div class="field"><label for="editPhone">رقم الموبايل</label><input class="control" id="editPhone" type="tel" dir="ltr" formControlName="phoneNumber">@if (editForm.controls.phoneNumber.invalid && editForm.controls.phoneNumber.touched) { <span class="field__error">اكتب رقم موبايل صحيح.</span> }</div><div class="field field--wide inline"><button class="btn btn--primary" type="submit" [disabled]="saving()">{{ saving() ? 'جاري الحفظ…' : 'حفظ التعديل' }}</button><button class="btn btn--secondary" type="button" (click)="editing.set(false)">إلغاء</button></div></form></section> }
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
  private readonly api = inject(PeopleApiService); private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''; private readonly notifications = inject(NotificationService); private readonly errors = inject(ApiErrorService); private readonly confirm = inject(ConfirmService);
  protected readonly loading = signal(true); protected readonly error = signal(false); protected readonly profile = signal<PersonProfileDto | null>(null); protected readonly editing = signal(false); protected readonly saving = signal(false);
  protected readonly editForm = new FormGroup({ fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }), phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9+ ]+$/), Validators.maxLength(30)] }) });
  constructor() { this.load(); }
  protected openEdit(data: PersonProfileDto): void { this.editForm.setValue({ fullName: data.person.fullName, phoneNumber: data.person.phoneNumber }); this.editing.set(true); }
  protected save(): void { this.editForm.markAllAsTouched(); if (this.editForm.invalid || this.saving()) return; this.saving.set(true); this.api.update(this.id, this.editForm.getRawValue()).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.notifications.success('تم تحديث بيانات الشخص.'); this.editing.set(false); this.load(); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected async deactivate(): Promise<void> { if (!await this.confirm.ask({ title: 'إلغاء الشخص', message: 'لن يتم حذف أي سجل مالي، ولا يمكن الإلغاء لو الشخص مشترك في جمعية حالية.', confirmLabel: 'تأكيد الإلغاء', danger: true })) return; this.api.deactivate(this.id).subscribe({ next: () => { this.notifications.success('تم إلغاء الشخص مع الاحتفاظ بالتاريخ.'); this.load(); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  private load(): void { this.loading.set(true); this.error.set(false); this.api.profile(this.id).subscribe({ next: (data) => { this.profile.set(data); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
}
