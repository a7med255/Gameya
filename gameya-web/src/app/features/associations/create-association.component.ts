import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AssociationDetailsDto, BusinessValidationError, PersonDto, PayoutRecipientType,
  SaveAssociationConfigurationRequest,
} from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { NotificationService } from '../../core/services/notification.service';
import { PeopleApiService } from '../../core/services/people-api.service';
import { AssociationEditorMembersComponent } from './association-editor-members.component';
import { EditorMember, EditorPayout, createClientKey } from './association-editor.models';
import { AssociationEditorScheduleComponent } from './association-editor-schedule.component';
import { AssociationEditorSummaryComponent } from './association-editor-summary.component';

@Component({
  selector: 'app-create-association',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AssociationEditorMembersComponent,
    AssociationEditorScheduleComponent, AssociationEditorSummaryComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main class="page association-editor">
      <a class="btn btn--ghost back" routerLink="/owner/associations">→ رجوع للجمعيات</a>
      <header class="editor-hero">
        <div><span class="eyebrow">محرر الجمعية</span><h1 class="page-title">{{ associationId ? 'تعديل مسودة الجمعية' : 'إنشاء جمعية جديدة' }}</h1>
          <p class="page-subtitle">كل الإعدادات في شاشة واحدة؛ احفظ مسودة في أي وقت أو ابدأ بعد اجتياز تحقق الخادم.</p></div>
        @if (loading()) { <span class="badge">جاري التحميل…</span> }
      </header>

      <div class="editor-layout">
        <div class="editor-main">
          <section class="editor-section card" aria-labelledby="basic-title">
            <div class="section-heading"><span class="section-index">١</span><h2 id="basic-title">البيانات الأساسية</h2></div>
            <form class="form-grid" [formGroup]="basicForm">
              <div class="field field--wide"><label for="associationName">اسم الجمعية</label>
                <input class="control" id="associationName" data-field="name" formControlName="name" placeholder="مثال: جمعية العيلة 2027">
                @if (basicForm.controls.name.invalid && basicForm.controls.name.touched) { <span class="field__error">اسم الجمعية مطلوب وبحد أقصى 200 حرف.</span> }
              </div>
              <div class="field"><label for="startDate">تاريخ البداية</label><input class="control" id="startDate" data-field="startDate" type="date" formControlName="startDate"></div>
              <div class="field"><label for="months">عدد الشهور</label><input class="control" id="months" data-field="numberOfMonths" type="number" min="1" max="120" formControlName="numberOfMonths"></div>
              <div class="field"><label for="contribution">الاشتراك الشهري</label><input class="control" id="contribution" data-field="monthlyContribution" type="number" min="0.01" step="0.01" formControlName="monthlyContribution"><span class="field__hint">جنيه مصري</span></div>
              <div class="calculated-date"><small>تاريخ النهاية المتوقع</small><strong>{{ endDateLabel() }}</strong></div>
            </form>
          </section>

          <app-association-editor-members [members]="members()" [people]="people()"
            [payouts]="payouts()" [contribution]="basicForm.controls.monthlyContribution.value"
            [months]="basicForm.controls.numberOfMonths.value"
            (addExisting)="addExistingMember($event)" (addNew)="addInlineMember($event)"
            (remove)="removeMember($event)" (move)="moveMember($event.from, $event.to)"
            (sharesChange)="changeShares($event.clientKey, $event.shareCount)" />

          <app-association-editor-schedule [members]="members()" [payouts]="payouts()" [people]="people()"
            [months]="basicForm.controls.numberOfMonths.value" (add)="addPayout($event)"
            (addAt)="addPayout('Membership', $event)" (change)="changePayout($event.index, $event.value)"
            (remove)="removePayout($event)" />
        </div>

        <app-association-editor-summary [members]="members()" [payouts]="payouts()" [people]="people()"
          [contribution]="basicForm.controls.monthlyContribution.value"
          [months]="basicForm.controls.numberOfMonths.value" [errors]="businessErrors()" />
      </div>

      @if (errorMessage()) { <div class="alert alert--error editor-error" role="alert">{{ errorMessage() }}</div> }
      <footer class="editor-actions">
        <div><strong>{{ members().length }} أعضاء · {{ payouts().length }} توزيعات</strong><small>الحفظ يتم كعملية واحدة؛ لن تُحفظ جمعية جزئية.</small></div>
        <div class="action-buttons">
          <button class="btn btn--secondary" type="button" [disabled]="saving()" (click)="save(false)">{{ saving() ? 'جاري الحفظ…' : 'حفظ كمسودة' }}</button>
          <button class="btn btn--primary" type="button" [disabled]="saving()" (click)="save(true)">حفظ وبدء الجمعية</button>
        </div>
      </footer>
    </main>
  `,
  styles: [`
    .association-editor{max-width:1440px;padding-bottom:7rem}.back{margin-bottom:.8rem}.editor-hero{display:flex;justify-content:space-between;align-items:flex-start;gap:1rem;margin-bottom:1.2rem}.eyebrow{color:var(--brand-700);font-size:.8rem;font-weight:850;letter-spacing:.08em}.editor-layout{display:grid;grid-template-columns:minmax(0,1fr) 20rem;gap:1rem;align-items:start}.editor-main{display:grid;gap:1rem}.editor-section{padding:1.1rem}.section-heading{display:flex;align-items:center;gap:.75rem;margin-bottom:1rem}.section-heading h2{margin:0;font-size:1.15rem}.section-index{display:grid;place-items:center;width:2rem;height:2rem;border-radius:.65rem;background:var(--brand-700);color:#fff;font-weight:800}.calculated-date{align-self:end;padding:.65rem .8rem;border-radius:.7rem;background:var(--surface-muted,#f7f5ef)}.calculated-date small{display:block;color:var(--ink-500)}.editor-actions{position:fixed;z-index:20;inset-inline:0;bottom:0;display:flex;justify-content:flex-end;align-items:center;gap:2rem;padding:.75rem max(1rem,calc((100vw - 1380px)/2));background:rgb(255 255 255 / .96);border-top:1px solid var(--line);box-shadow:0 -8px 24px rgb(20 33 28 / .08);backdrop-filter:blur(10px)}.editor-actions>div:first-child small{display:block;color:var(--ink-500)}.action-buttons{display:flex;gap:.7rem}.editor-error{position:fixed;z-index:25;bottom:5.5rem;left:50%;transform:translateX(-50%);max-width:42rem;width:calc(100% - 2rem)}@media(max-width:980px){.editor-layout{grid-template-columns:1fr}.editor-actions{justify-content:space-between}}@media(max-width:700px){.association-editor{padding-bottom:10rem}.editor-hero{flex-direction:column}.editor-actions{align-items:stretch;flex-direction:column;gap:.55rem}.action-buttons{display:grid;grid-template-columns:1fr 1fr}.editor-error{bottom:9.5rem}}
  `],
})
export class CreateAssociationComponent {
  private readonly associationsApi = inject(AssociationsApiService);
  private readonly peopleApi = inject(PeopleApiService);
  private readonly notifications = inject(NotificationService);
  private readonly errors = inject(ApiErrorService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly associationId = this.route.snapshot.paramMap.get('id');
  protected readonly people = signal<readonly PersonDto[]>([]);
  protected readonly members = signal<readonly EditorMember[]>([]);
  protected readonly payouts = signal<readonly EditorPayout[]>([]);
  protected readonly businessErrors = signal<readonly BusinessValidationError[]>([]);
  protected readonly errorMessage = signal('');
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  private dirty = false;

  protected readonly basicForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    startDate: new FormControl(this.today(), { nonNullable: true, validators: [Validators.required] }),
    numberOfMonths: new FormControl(12, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(120)] }),
    monthlyContribution: new FormControl(1000, { nonNullable: true, validators: [Validators.required, Validators.min(.01)] }),
  });

  constructor() {
    this.peopleApi.list('', 1, 100).subscribe({ next: (result) => this.people.set(result.items) });
    this.basicForm.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.dirty = true;
      const months = Math.max(1, this.basicForm.controls.numberOfMonths.value);
      this.payouts.update((items) => items.map((item) => ({ ...item, periodNumber: Math.min(item.periodNumber, months) })));
    });
    if (this.associationId) this.loadDraft(this.associationId);
  }

  protected addExistingMember(personId: string): void {
    if (this.members().some((member) => member.personId === personId)) return;
    this.addMember({ clientKey: createClientKey(), personId, fullName: null, phoneNumber: null, shareCount: 1, displayOrder: this.members().length });
  }
  protected addInlineMember(value: { fullName: string; phoneNumber: string }): void {
    this.addMember({ clientKey: createClientKey(), personId: null, fullName: value.fullName, phoneNumber: value.phoneNumber, shareCount: 1, displayOrder: this.members().length });
  }
  protected removeMember(clientKey: string): void {
    this.members.update((items) => items.filter((item) => item.clientKey !== clientKey)
      .map((item, index) => ({ ...item, displayOrder: index })));
    this.payouts.update((items) => items.filter((item) => item.memberClientKey !== clientKey)
      .map((item) => item.beneficiaryMemberClientKey === clientKey ? { ...item, beneficiaryMemberClientKey: null } : item));
    this.markDirty();
  }
  protected moveMember(from: number, to: number): void {
    const items = [...this.members()];
    const [moved] = items.splice(from, 1);
    if (!moved) return;
    items.splice(to, 0, moved);
    this.members.set(items.map((item, index) => ({ ...item, displayOrder: index })));
    this.markDirty();
  }
  protected changeShares(clientKey: string, shareCount: number): void {
    this.members.update((items) => items.map((item) => item.clientKey === clientKey ? { ...item, shareCount } : item));
    this.markDirty();
  }
  protected addPayout(type: PayoutRecipientType, requestedPeriod?: number): void {
    const months = Math.max(1, this.basicForm.controls.numberOfMonths.value);
    const firstMember = this.members().find((member) => this.scheduledShares(member.clientKey) < member.shareCount) ?? this.members()[0];
    if (type === 'Membership' && !firstMember) return;
    const firstEmpty = Array.from({ length: months }, (_, index) => index + 1)
      .find((period) => !this.payouts().some((payout) => payout.periodNumber === period)) ?? 1;
    this.payouts.update((items) => [...items, {
      periodNumber: requestedPeriod ?? firstEmpty,
      recipientType: type,
      memberClientKey: type === 'Membership' ? firstMember?.clientKey ?? null : null,
      shareCount: type === 'Membership' ? .5 : 1,
      beneficiaryMemberClientKey: null,
    }]);
    this.markDirty();
  }
  protected changePayout(index: number, value: EditorPayout): void {
    this.payouts.update((items) => items.map((item, itemIndex) => itemIndex === index ? value : item));
    this.markDirty();
  }
  protected removePayout(index: number): void {
    this.payouts.update((items) => items.filter((_, itemIndex) => itemIndex !== index));
    this.markDirty();
  }
  protected endDateLabel(): string {
    const value = this.basicForm.controls.startDate.value;
    if (!value) return '—';
    const date = new Date(`${value}T12:00:00`);
    date.setMonth(date.getMonth() + Math.max(1, this.basicForm.controls.numberOfMonths.value) - 1);
    return new Intl.DateTimeFormat('ar-EG', { year: 'numeric', month: 'long', day: 'numeric' }).format(date);
  }
  protected save(startAssociation: boolean): void {
    this.basicForm.markAllAsTouched();
    if (this.basicForm.invalid || this.saving()) return;
    this.saving.set(true);
    this.errorMessage.set('');
    this.businessErrors.set([]);
    const request: SaveAssociationConfigurationRequest = {
      ...this.basicForm.getRawValue(), members: this.members(), payouts: this.payouts(), startAssociation,
    };
    const operation = this.associationId
      ? this.associationsApi.saveConfiguration(this.associationId, request)
      : this.associationsApi.createConfiguration(request);
    operation.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: (association) => {
        this.dirty = false;
        this.notifications.success(startAssociation ? 'تم حفظ الجمعية وبدؤها بنجاح.' : 'تم حفظ مسودة الجمعية بنجاح.');
        void this.router.navigate(['/owner/associations', association.id]);
      },
      error: (error: unknown) => {
        const businessErrors = this.errors.businessErrors(error);
        this.businessErrors.set(businessErrors);
        this.errorMessage.set(businessErrors.length ? `يوجد ${businessErrors.length} أخطاء في الإعداد. راجع ملخص التحقق.` : this.errors.message(error));
      },
    });
  }
  hasUnsavedChanges(): boolean { return this.dirty && !this.saving(); }

  private addMember(member: EditorMember): void {
    const months = Math.max(1, this.basicForm.controls.numberOfMonths.value);
    this.members.update((items) => [...items, member]);
    this.payouts.update((items) => [...items, {
      periodNumber: member.displayOrder % months + 1,
      recipientType: 'Membership', memberClientKey: member.clientKey, shareCount: 1,
      beneficiaryMemberClientKey: null,
    }]);
    this.markDirty();
  }
  private scheduledShares(clientKey: string): number {
    return this.payouts().filter((payout) => payout.recipientType === 'Membership' && payout.memberClientKey === clientKey)
      .reduce((sum, payout) => sum + payout.shareCount, 0);
  }
  private loadDraft(id: string): void {
    this.loading.set(true);
    this.associationsApi.editor(id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (editor) => {
        this.applyAssociation(editor.association);
        this.businessErrors.set(editor.validationErrors);
      },
      error: (error: unknown) => this.errorMessage.set(this.errors.message(error)),
    });
  }
  private applyAssociation(association: AssociationDetailsDto): void {
    this.basicForm.setValue({ name: association.name, startDate: association.startDate,
      numberOfMonths: association.numberOfMonths, monthlyContribution: association.monthlyContribution }, { emitEvent: false });
    const ordered = [...association.memberships].sort((a, b) => a.displayOrder - b.displayOrder);
    const members = ordered.map((member, index): EditorMember => ({ clientKey: member.id, personId: member.personId,
      fullName: null, phoneNumber: null, shareCount: member.shareCount, displayOrder: index }));
    this.members.set(members);
    this.payouts.set(association.payouts.map((payout): EditorPayout => ({
      periodNumber: payout.periodNumber, recipientType: payout.recipientType,
      memberClientKey: payout.membershipId,
      shareCount: payout.shareCount,
      beneficiaryMemberClientKey: members.find((member) => member.personId === payout.beneficiaryPersonId)?.clientKey ?? null,
    })));
    this.dirty = false;
  }
  private markDirty(): void { this.dirty = true; this.businessErrors.set([]); }
  private today(): string { return new Date().toISOString().slice(0, 10); }
}
