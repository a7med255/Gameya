import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged, finalize, startWith, switchMap } from 'rxjs';
import { PagedResult, PersonDto } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { NotificationService } from '../../core/services/notification.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';
import { StatusBadgeComponent } from '../../shared/ui/status-badge.component';

@Component({
  selector: 'app-people-list', standalone: true, imports: [ReactiveFormsModule, RouterLink, ArabicDatePipe, EmptyStateComponent, LoadingStateComponent, StatusBadgeComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <header class="page-header"><div><h1 class="page-title">الأشخاص</h1><p class="page-subtitle">كل شخص بيتسجل مرة واحدة حتى لو مشترك في أكتر من جمعية.</p></div><button class="btn btn--primary" type="button" (click)="showCreate.set(!showCreate())">+ إضافة شخص</button></header>
      @if (showCreate()) {
        <section class="card" style="margin-bottom:1rem">
          <div class="split"><h2 class="section-title">بيانات الشخص</h2><button class="icon-btn" type="button" aria-label="إغلاق" (click)="showCreate.set(false)">×</button></div>
          <form class="form-grid" [formGroup]="createForm" (ngSubmit)="create()">
            <div class="field"><label for="fullName">الاسم بالكامل</label><input class="control" id="fullName" autocomplete="name" formControlName="fullName">@if (createForm.controls.fullName.invalid && createForm.controls.fullName.touched) { <span class="field__error">الاسم مطلوب.</span> }</div>
            <div class="field"><label for="phone">رقم الموبايل</label><input class="control" id="phone" type="tel" dir="ltr" inputmode="tel" autocomplete="tel" formControlName="phoneNumber">@if (createForm.controls.phoneNumber.invalid && createForm.controls.phoneNumber.touched) { <span class="field__error">من فضلك أدخل رقم موبايل صحيح.</span> }</div>
            <div class="field field--wide inline"><button class="btn btn--primary" type="submit" [disabled]="saving()">{{ saving() ? 'جاري الحفظ…' : 'حفظ الشخص' }}</button><button class="btn btn--secondary" type="button" (click)="showCreate.set(false)">إلغاء</button></div>
          </form>
        </section>
      }
      <section class="card card--flat" style="margin-bottom:1rem"><div class="search-box"><span class="search-box__icon" aria-hidden="true">⌕</span><label class="sr-only" for="people-search">ابحث بالاسم أو كود المشترك</label><input class="control" id="people-search" type="search" placeholder="ابحث بالاسم أو كود المشترك…" [formControl]="search"></div></section>
      @if (loading()) { <app-loading-state /> }
      @else if (error()) { <app-empty-state icon="!" title="تعذر تحميل الأشخاص" message="حاول تاني بعد شوية." actionLabel="إعادة المحاولة" (action)="reload()" /> }
      @else if (result().items.length === 0) { <app-empty-state icon="أ" title="لسه مفيش أشخاص مضافين" message="أضف أول شخص علشان تبدأ تكوّن جمعياتك." actionLabel="إضافة شخص" (action)="showCreate.set(true)" /> }
      @else {
        <div class="table-wrap desktop-table"><table class="data-table"><thead><tr><th>الاسم</th><th>رقم الموبايل</th><th>كود المشترك</th><th>الحالة</th><th>تاريخ الإضافة</th><th><span class="sr-only">الإجراء</span></th></tr></thead><tbody>@for (person of result().items; track person.id) { <tr><td><strong>{{ person.fullName }}</strong></td><td dir="ltr">{{ person.phoneNumber }}</td><td><span class="code">{{ person.subscriberCode }}</span></td><td><app-status-badge [status]="person.isActive ? 'Active' : 'Cancelled'" /></td><td>{{ person.createdAtUtc | arabicDate }}</td><td><a class="btn btn--ghost" [routerLink]="['/owner/people', person.id]">عرض الملف</a></td></tr> }</tbody></table></div>
        <div class="mobile-list">@for (person of result().items; track person.id) { <article class="mobile-card"><div class="split"><strong>{{ person.fullName }}</strong><app-status-badge [status]="person.isActive ? 'Active' : 'Cancelled'" /></div><dl class="detail-list" style="margin-top:.75rem"><div><dt>الموبايل</dt><dd dir="ltr">{{ person.phoneNumber }}</dd></div><div><dt>كود المشترك</dt><dd class="code">{{ person.subscriberCode }}</dd></div></dl><a class="btn btn--secondary btn--block" style="margin-top:1rem" [routerLink]="['/owner/people', person.id]">عرض الملف</a></article> }</div>
        <div class="pagination"><button class="btn btn--secondary" type="button" [disabled]="result().page <= 1" (click)="changePage(-1)">السابق</button><span>صفحة {{ result().page }} من {{ totalPages() }}</span><button class="btn btn--secondary" type="button" [disabled]="result().page >= totalPages()" (click)="changePage(1)">التالي</button></div>
      }
    </div>
  `,
})
export class PeopleListComponent {
  private readonly api = inject(PeopleApiService); private readonly notifications = inject(NotificationService); private readonly errors = inject(ApiErrorService); private readonly router = inject(Router);
  protected readonly search = new FormControl('', { nonNullable: true });
  protected readonly result = signal<PagedResult<PersonDto>>({ items: [], page: 1, pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false });
  protected readonly loading = signal(true); protected readonly error = signal(false); protected readonly showCreate = signal(false); protected readonly saving = signal(false);
  protected readonly createForm = new FormGroup({ fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }), phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[0-9+ ]+$/), Validators.maxLength(30)] }) });
  constructor() { this.search.valueChanges.pipe(startWith(''), debounceTime(350), distinctUntilChanged(), switchMap((search) => { this.loading.set(true); this.error.set(false); return this.api.list(search, 1); })).subscribe({ next: (result) => { this.result.set(result); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
  protected totalPages(): number { return Math.max(1, Math.ceil(this.result().totalCount / this.result().pageSize)); }
  protected changePage(delta: number): void { const page = this.result().page + delta; this.loading.set(true); this.api.list(this.search.value, page).subscribe({ next: (result) => { this.result.set(result); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
  protected reload(): void { this.loading.set(true); this.error.set(false); this.api.list(this.search.value, 1).subscribe({ next: (result) => { this.result.set(result); this.loading.set(false); }, error: () => { this.error.set(true); this.loading.set(false); } }); }
  protected create(): void { this.createForm.markAllAsTouched(); if (this.createForm.invalid || this.saving()) return; this.saving.set(true); this.api.create(this.createForm.getRawValue()).pipe(finalize(() => this.saving.set(false))).subscribe({ next: (person) => { this.notifications.success('تم إضافة الشخص بنجاح.'); this.createForm.reset(); this.showCreate.set(false); void this.router.navigate(['/owner/people', person.id]); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
}
