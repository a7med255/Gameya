import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs';
import {
  AssociationSummaryDto, PagedResult, PaymentUserDto, PaymentUserInvitationDto,
  PaymentUserInvitationStatus,
} from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { PaymentUsersApiService } from '../../core/services/payment-users-api.service';
import { ArabicDatePipe } from '../../shared/pipes/arabic-date.pipe';
import { EmptyStateComponent } from '../../shared/ui/empty-state.component';
import { LoadingStateComponent } from '../../shared/ui/loading-state.component';

@Component({
  selector: 'app-settings', standalone: true,
  imports: [ReactiveFormsModule, ArabicDatePipe, EmptyStateComponent, LoadingStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page settings-page">
      <header class="page-header"><div><h1 class="page-title">إدارة مسجلي المدفوعات</h1><p class="page-subtitle">إرسال الدعوات فورًا، إلغاء الروابط النشطة، ومراجعة صلاحيات الفريق.</p></div></header>

      <section class="card"><h2 class="section-title">دعوة مسجل مدفوعات</h2><p class="muted">الدعوة بتتبعت فورًا، والرابط يظل صالحًا لحد القبول أو إلغائه منك، ويُستخدم مرة واحدة.</p>
        <form class="stack" [formGroup]="form" (ngSubmit)="invite()">
          <div class="field"><label for="inviteEmail">البريد الإلكتروني</label><input class="control" id="inviteEmail" type="email" dir="ltr" autocomplete="email" formControlName="email">@if (form.controls.email.invalid && form.controls.email.touched) { <span class="field__error">اكتب بريد إلكتروني صحيح.</span> }</div>
          <fieldset class="association-choices"><legend>الجمعيات المسموح بها</legend>
            @for (association of associations().items; track association.id) { <label><input #accessCheck type="checkbox" [checked]="selectedIds().includes(association.id)" (change)="toggle(association.id, accessCheck.checked)"><span>{{ association.name }}</span></label> }
            @if (associations().items.length === 0) { <p class="muted">أنشئ جمعية الأول علشان تقدر تبعت دعوة.</p> }
          </fieldset>
          @if (associations().totalPages > 1) { <div class="pagination compact"><button class="btn btn--ghost" type="button" [disabled]="!associations().hasPrevious" (click)="loadAssociations(associations().page - 1)">السابق</button><span>{{ associations().page }} / {{ associations().totalPages }}</span><button class="btn btn--ghost" type="button" [disabled]="!associations().hasNext" (click)="loadAssociations(associations().page + 1)">التالي</button></div> }
          <button class="btn btn--primary" type="submit" [disabled]="saving() || selectedIds().length === 0">{{ saving() ? 'جاري إرسال الدعوة…' : 'إرسال الدعوة الآن' }}</button>
        </form>
      </section>

      <section class="card" style="margin-top:1rem"><div class="split"><div><h2 class="section-title">الدعوات</h2><p class="muted">السجل محفوظ حتى بعد القبول أو الإلغاء.</p></div><input class="control team-search" type="search" placeholder="بحث بالبريد…" [formControl]="teamSearch"></div>
        @if (loadingInvitations()) { <app-loading-state /> }
        @else if (invitations().items.length === 0) { <app-empty-state icon="د" title="مفيش دعوات" message="الدعوات اللي تبعتها هتظهر هنا." /> }
        @else { <div class="stack">@for (invitation of invitations().items; track invitation.id) { <article class="team-row"><div><strong dir="ltr">{{ invitation.email }}</strong><div class="muted">أُرسلت {{ invitation.createdAtUtc | arabicDate:true }} · {{ invitation.associationIds.length }} جمعية</div></div><div class="inline"><span class="invite-status" [attr.data-status]="invitation.status">{{ invitationStatus(invitation.status) }}</span>@if (invitation.status === 'Active') { <button class="btn btn--danger" type="button" (click)="revoke(invitation)">إلغاء الدعوة</button> }</div></article> }</div>
          <div class="pagination"><button class="btn btn--secondary" type="button" [disabled]="!invitations().hasPrevious" (click)="loadInvitations(invitations().page - 1)">السابق</button><span>صفحة {{ invitations().page }} من {{ maxPage(invitations()) }}</span><button class="btn btn--secondary" type="button" [disabled]="!invitations().hasNext" (click)="loadInvitations(invitations().page + 1)">التالي</button></div>
        }
      </section>

      <section class="card" style="margin-top:1rem"><h2 class="section-title">الحسابات النشطة</h2>
        @if (loadingUsers()) { <app-loading-state /> }
        @else if (users().items.length === 0) { <app-empty-state icon="ف" title="مفيش مسجلين مدفوعات" message="الحساب بيظهر هنا بعد قبول الدعوة." /> }
        @else { <div class="stack">@for (user of users().items; track user.id) { <article class="team-row"><div><strong>{{ user.displayName }}</strong><div class="muted" dir="ltr">{{ user.email }}</div><small class="muted">{{ user.associationIds.length }} جمعية متاحة</small></div><div class="inline"><span>{{ user.isActive ? 'نشط' : 'موقوف' }}</span>@if (user.isActive) { <button class="btn btn--danger" type="button" (click)="deactivate(user)">إزالة الوصول</button> }</div></article> }</div>
          <div class="pagination"><button class="btn btn--secondary" type="button" [disabled]="!users().hasPrevious" (click)="loadUsers(users().page - 1)">السابق</button><span>صفحة {{ users().page }} من {{ maxPage(users()) }}</span><button class="btn btn--secondary" type="button" [disabled]="!users().hasNext" (click)="loadUsers(users().page + 1)">التالي</button></div>
        }
      </section>
    </div>
  `,
  styles: [`.settings-page{max-width:900px}.association-choices{border:1px solid var(--line);border-radius:.8rem;padding:1rem;display:grid;grid-template-columns:repeat(auto-fit,minmax(210px,1fr));gap:.7rem}.association-choices legend{font-weight:800;padding-inline:.4rem}.association-choices label{display:flex;align-items:center;gap:.6rem;min-height:2.5rem}.team-search{max-width:17rem}.team-row{display:flex;align-items:center;justify-content:space-between;gap:1rem;padding:1rem;border:1px solid var(--line);border-radius:.8rem}.invite-status{font-weight:800;color:var(--ink-500)}.invite-status[data-status="Active"]{color:var(--brand-700)}.invite-status[data-status="Revoked"],.invite-status[data-status="Expired"]{color:var(--danger)}.compact{margin:0}@media(max-width:600px){.team-row,.split{align-items:stretch;flex-direction:column}.team-search{max-width:none;width:100%}.team-row .inline{justify-content:space-between}}`],
})
export class SettingsComponent {
  private readonly associationsApi = inject(AssociationsApiService);
  private readonly paymentUsersApi = inject(PaymentUsersApiService);
  private readonly notifications = inject(NotificationService);
  private readonly errors = inject(ApiErrorService);
  private readonly confirm = inject(ConfirmService);
  protected readonly associations = signal<PagedResult<AssociationSummaryDto>>(this.emptyPage<AssociationSummaryDto>());
  protected readonly invitations = signal<PagedResult<PaymentUserInvitationDto>>(this.emptyPage<PaymentUserInvitationDto>());
  protected readonly users = signal<PagedResult<PaymentUserDto>>(this.emptyPage<PaymentUserDto>());
  protected readonly selectedIds = signal<readonly string[]>([]);
  protected readonly saving = signal(false);
  protected readonly loadingInvitations = signal(true);
  protected readonly loadingUsers = signal(true);
  protected readonly teamSearch = new FormControl('', { nonNullable: true });
  protected readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }) });

  constructor() {
    this.loadAssociations(1); this.loadInvitations(1); this.loadUsers(1);
    this.teamSearch.valueChanges.pipe(debounceTime(350), distinctUntilChanged()).subscribe(() => {
      this.loadInvitations(1); this.loadUsers(1);
    });
  }

  protected toggle(id: string, checked: boolean): void { this.selectedIds.update((ids) => checked ? [...ids, id] : ids.filter((value) => value !== id)); }
  protected loadAssociations(page: number): void { this.associationsApi.list('', '', page, 20).subscribe({ next: (result) => this.associations.set(result), error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected loadInvitations(page: number): void { this.loadingInvitations.set(true); this.paymentUsersApi.invitations(this.teamSearch.value, page).pipe(finalize(() => this.loadingInvitations.set(false))).subscribe({ next: (result) => this.invitations.set(result), error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected loadUsers(page: number): void { this.loadingUsers.set(true); this.paymentUsersApi.list(this.teamSearch.value, page).pipe(finalize(() => this.loadingUsers.set(false))).subscribe({ next: (result) => this.users.set(result), error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected invite(): void { this.form.markAllAsTouched(); if (this.form.invalid || this.selectedIds().length === 0 || this.saving()) return; this.saving.set(true); this.paymentUsersApi.invite({ email: this.form.controls.email.value, associationIds: this.selectedIds() }).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.notifications.success('تم إرسال الدعوة فورًا.'); this.form.reset(); this.selectedIds.set([]); this.loadInvitations(1); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected async revoke(invitation: PaymentUserInvitationDto): Promise<void> { if (!await this.confirm.ask({ title: 'إلغاء الدعوة', message: 'الرابط القديم هيتوقف فورًا ومش هيقدر حد يستخدمه.', confirmLabel: 'إلغاء الرابط', danger: true })) return; this.paymentUsersApi.revokeInvitation(invitation.id).subscribe({ next: () => { this.notifications.success('تم إلغاء رابط الدعوة.'); this.loadInvitations(this.invitations().page); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected async deactivate(user: PaymentUserDto): Promise<void> { if (!await this.confirm.ask({ title: 'إزالة وصول مسجل المدفوعات', message: 'هيتم سحب وصوله لجمعياتك مع الاحتفاظ بسجل العمليات السابقة.', confirmLabel: 'إزالة الوصول', danger: true })) return; this.paymentUsersApi.deactivate(user.id).subscribe({ next: () => { this.notifications.success('تمت إزالة الوصول مع الاحتفاظ بالسجل.'); this.loadUsers(this.users().page); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected invitationStatus(status: PaymentUserInvitationStatus): string { return ({ Active: 'نشطة', Accepted: 'تم قبولها', Revoked: 'ملغاة', Expired: 'منتهية' })[status]; }
  protected maxPage(page: PagedResult<unknown>): number { return Math.max(1, page.totalPages); }
  private emptyPage<T>(): PagedResult<T> { return { items: [], page: 1, pageNumber: 1, pageSize: 20, totalCount: 0, totalPages: 0, hasNext: false, hasPrevious: false }; }
}
