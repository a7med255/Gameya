import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { concatMap, finalize, from, switchMap, tap, toArray } from 'rxjs';
import { MembershipKind, PersonDto, PayoutRecipientType } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { NotificationService } from '../../core/services/notification.service';
import { PeopleApiService } from '../../core/services/people-api.service';

type MemberGroup = FormGroup<{
  personId: FormControl<string>;
  shareCount: FormControl<number>;
  kind: FormControl<MembershipKind>;
}>;
type ScheduleGroup = FormGroup<{
  periodNumber: FormControl<number>;
  recipientType: FormControl<PayoutRecipientType>;
  personId: FormControl<string>;
  shareCount: FormControl<number>;
}>;

@Component({
  selector: 'app-create-association', standalone: true, imports: [ReactiveFormsModule, RouterLink], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page wizard-page">
      <a class="btn btn--ghost" routerLink="/owner/associations">→ رجوع للجمعيات</a>
      <header class="page-header"><div><h1 class="page-title">إنشاء جمعية جديدة</h1><p class="page-subtitle">كمّل الخطوات، وهتفضل الجمعية مسودة لحد ما تراجعها وتبدأها.</p></div></header>
      <ol class="stepper" aria-label="خطوات إنشاء الجمعية">@for (label of stepLabels; track label; let index = $index) { <li [class.stepper__item--active]="step() === index + 1" [class.stepper__item--done]="step() > index + 1"><span>{{ index + 1 }}</span><small>{{ label }}</small></li> }</ol>
      <section class="card wizard-card">
        @if (step() === 1) {
          <h2>البيانات الأساسية</h2>
          <form class="form-grid" [formGroup]="basicForm">
            <div class="field field--wide"><label for="associationName">اسم الجمعية</label><input class="control" id="associationName" formControlName="name" placeholder="مثال: جمعية العيلة 2027">@if (basicForm.controls.name.invalid && basicForm.controls.name.touched) { <span class="field__error">اسم الجمعية مطلوب.</span> }</div>
            <div class="field"><label for="startDate">تاريخ البداية</label><input class="control" id="startDate" type="date" formControlName="startDate"><span class="field__hint">تاريخ النهاية بيحسبه النظام.</span></div>
            <div class="field"><label for="months">عدد الشهور</label><input class="control" id="months" type="number" min="1" max="120" inputmode="numeric" formControlName="numberOfMonths"></div>
            <div class="field field--wide"><label for="contribution">قيمة الاشتراك الشهري</label><input class="control" id="contribution" type="number" min="0.01" step="0.01" inputmode="decimal" formControlName="monthlyContribution"><span class="field__hint">بالجنيه المصري</span></div>
          </form>
        }
        @if (step() === 2) {
          <h2>اختيار الأعضاء</h2><p class="muted">اختار الأشخاص اللي هيشتركوا. تقدر تضيف أشخاص جدد من صفحة الأشخاص.</p>
          @if (peopleLoading()) { <p class="muted">جاري تحميل الأشخاص…</p> } @else { <div class="choice-grid">@for (person of people(); track person.id) { <label class="choice-card"><input #memberCheck type="checkbox" [checked]="hasMember(person.id)" (change)="toggleMember(person, memberCheck.checked)"><span><strong>{{ person.fullName }}</strong><small dir="ltr">{{ person.phoneNumber }}</small></span><span class="code">{{ person.subscriberCode }}</span></label> }</div> }
          @if (members.length === 0) { <p class="alert alert--warning">اختار عضو واحد على الأقل علشان تكمّل.</p> }
        }
        @if (step() === 3) {
          <h2>الحصص</h2><p class="muted">الحصص بتزيد بنص حصة. «سلف» شخص حقيقي لكن مش عليه التزام دفع.</p>
          <form [formGroup]="membersContainer"><div class="stack" formArrayName="members">@for (member of members.controls; track member.controls.personId.value; let index = $index) { <div class="member-row" [formGroupName]="index"><strong>{{ personName(member.controls.personId.value) }}</strong><div class="field"><label [for]="'kind-' + index">النوع</label><select class="control" [id]="'kind-' + index" formControlName="kind"><option value="Paying">عضو دافع</option><option value="Advance">سلف</option></select></div><div class="field"><label [for]="'shares-' + index">عدد الحصص</label><input class="control" [id]="'shares-' + index" type="number" min="0.5" step="0.5" formControlName="shareCount"></div></div> }</div></form>
        }
        @if (step() === 4) {
          <div class="split"><div><h2>جدول القبض</h2><p class="muted">ضيف كل عضو في الفترة المناسبة. ممكن أكتر من مستفيد في نفس الفترة.</p></div><button class="btn btn--secondary" type="button" (click)="addMemberPayout()">+ إضافة قبض</button></div>
          <form [formGroup]="scheduleContainer"><div class="stack" formArrayName="schedule">@for (entry of memberPayoutControls(); track entry; let index = $index) { <div class="schedule-row" [formGroupName]="schedule.controls.indexOf(entry)"><div class="field"><label>الفترة</label><select class="control" formControlName="periodNumber">@for (period of periods(); track period) { <option [value]="period">الفترة {{ period }}</option> }</select></div><div class="field"><label>المستفيد</label><select class="control" formControlName="personId">@for (member of members.controls; track member.controls.personId.value) { <option [value]="member.controls.personId.value">{{ personName(member.controls.personId.value) }}</option> }</select></div><div class="field"><label>الحصص</label><input class="control" type="number" min="0.5" step="0.5" formControlName="shareCount"></div><button class="icon-btn" type="button" aria-label="حذف" (click)="removeSchedule(schedule.controls.indexOf(entry))">×</button></div> }</div></form>
          @if (memberPayoutControls().length === 0) { <p class="alert alert--warning">لسه مضفتش مواعيد قبض للأعضاء.</p> }
        }
        @if (step() === 5) {
          <div class="split"><div><h2>بدون اسم</h2><p class="muted">ده حجز احتياطي مش شخص، حصة واحدة، والدفع غير مطلوب.</p></div><button class="btn btn--secondary" type="button" (click)="addReserve()">+ إضافة احتياطي</button></div>
          <form [formGroup]="scheduleContainer"><div class="stack" formArrayName="schedule">@for (entry of reserveControls(); track entry) { <div class="schedule-row" [formGroupName]="schedule.controls.indexOf(entry)"><div><span class="badge badge--warning">بدون اسم</span><div class="muted">نوع: احتياطي · الحصة: 1</div></div><div class="field"><label>الفترة</label><select class="control" formControlName="periodNumber">@for (period of periods(); track period) { <option [value]="period">الفترة {{ period }}</option> }</select></div><button class="icon-btn" type="button" aria-label="حذف" (click)="removeSchedule(schedule.controls.indexOf(entry))">×</button></div> }</div></form>
          @if (reserveControls().length === 0) { <p class="muted">مفيش خانات «بدون اسم». الخطوة اختيارية.</p> }
        }
        @if (step() === 6) {
          <h2>المراجعة</h2><p class="muted">الأرقام دي للعرض والمراجعة فقط. الخادم هو المسؤول عن التحقق والحساب النهائي.</p>
          <div class="stat-grid"><article class="card card--flat stat"><span class="stat__label">إجمالي التزام الأعضاء</span><strong>يُحسب ويُعتمد على الخادم</strong></article><article class="card card--flat stat"><span class="stat__label">إجمالي القبض المطلوب</span><strong>يُحسب ويُعتمد على الخادم</strong></article><article class="card card--flat stat"><span class="stat__label">بدون اسم</span><strong class="stat__value">{{ reserveControls().length }}</strong></article><article class="card card--flat stat"><span class="stat__label">سلف</span><strong class="stat__value">{{ advanceCount() }}</strong></article></div>
          <div class="alert" [class.alert--info]="scheduleLooksBalanced()" [class.alert--warning]="!scheduleLooksBalanced()" style="margin-top:1rem"><strong>حالة المراجعة:</strong> {{ scheduleLooksBalanced() ? 'الحصص الظاهرة متوازنة مبدئيًا.' : 'في عضو حصص قبضه لا تساوي حصص اشتراكه. راجع الجدول.' }}</div>
          @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
        }
        <footer class="wizard-actions"><button class="btn btn--secondary" type="button" [disabled]="step() === 1 || saving()" (click)="previous()">السابق</button>@if (step() < 6) { <button class="btn btn--primary" type="button" (click)="next()">التالي</button> } @else { <button class="btn btn--primary" type="button" [disabled]="saving()" (click)="save()">{{ saving() ? 'جاري إنشاء الجمعية…' : 'إنشاء الجمعية كمسودة' }}</button> }</footer>
      </section>
    </div>
  `,
  styles: [`.wizard-page{max-width:1100px}.stepper{list-style:none;padding:0;display:grid;grid-template-columns:repeat(6,1fr);gap:.5rem;margin:1rem 0 1.5rem}.stepper__item{display:grid;justify-items:center;gap:.35rem;color:var(--ink-500);text-align:center}.stepper__item span{width:2.2rem;height:2.2rem;display:grid;place-items:center;border:1px solid var(--line);border-radius:50%;background:#fff;font-weight:800}.stepper__item--active span,.stepper__item--done span{color:#fff;background:var(--brand-700);border-color:var(--brand-700)}.stepper__item--active{color:var(--brand-800)}.wizard-card{min-height:28rem}.wizard-actions{display:flex;justify-content:space-between;gap:1rem;margin-top:2rem;padding-top:1rem;border-top:1px solid var(--line)}.choice-grid{display:grid;grid-template-columns:repeat(2,1fr);gap:.75rem}.choice-card{display:grid;grid-template-columns:auto 1fr auto;gap:.75rem;align-items:center;padding:1rem;border:1px solid var(--line);border-radius:.8rem}.choice-card:has(input:checked){border-color:var(--brand-600);background:var(--brand-50)}.choice-card small{display:block;color:var(--ink-500)}.member-row,.schedule-row{display:grid;grid-template-columns:minmax(10rem,1fr) repeat(2,minmax(8rem,.7fr)) auto;gap:1rem;align-items:end;padding:1rem;border:1px solid var(--line);border-radius:.8rem}@media(max-width:767px){.stepper small{display:none}.choice-grid{grid-template-columns:1fr}.member-row,.schedule-row{grid-template-columns:1fr}.wizard-card{padding:1rem}}`],
})
export class CreateAssociationComponent {
  private readonly associationsApi = inject(AssociationsApiService); private readonly peopleApi = inject(PeopleApiService); private readonly notifications = inject(NotificationService); private readonly errors = inject(ApiErrorService); private readonly router = inject(Router);
  protected readonly stepLabels = ['البيانات', 'الأعضاء', 'الحصص', 'القبض', 'الاحتياطي', 'المراجعة']; protected readonly step = signal(1); protected readonly people = signal<readonly PersonDto[]>([]); protected readonly peopleLoading = signal(true); protected readonly saving = signal(false); protected readonly error = signal('');
  protected readonly basicForm = new FormGroup({ name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }), startDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }), numberOfMonths: new FormControl(12, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(120)] }), monthlyContribution: new FormControl(1000, { nonNullable: true, validators: [Validators.required, Validators.min(.01)] }) });
  protected readonly members = new FormArray<MemberGroup>([]); protected readonly membersContainer = new FormGroup({ members: this.members });
  protected readonly schedule = new FormArray<ScheduleGroup>([]); protected readonly scheduleContainer = new FormGroup({ schedule: this.schedule });
  constructor() { this.peopleApi.list('', 1, 100).subscribe({ next: (result) => { this.people.set(result.items); this.peopleLoading.set(false); }, error: () => this.peopleLoading.set(false) }); }
  protected hasMember(id: string): boolean { return this.members.controls.some((x) => x.controls.personId.value === id); }
  protected personName(id: string): string { return this.people().find((person) => person.id === id)?.fullName ?? 'عضو'; }
  protected periods(): readonly number[] { return Array.from({ length: Math.max(1, this.basicForm.controls.numberOfMonths.value) }, (_, index) => index + 1); }
  protected advanceCount(): number { return this.members.controls.filter((x) => x.controls.kind.value === 'Advance').length; }
  protected memberPayoutControls(): readonly ScheduleGroup[] { return this.schedule.controls.filter((x) => x.controls.recipientType.value === 'Membership'); }
  protected reserveControls(): readonly ScheduleGroup[] { return this.schedule.controls.filter((x) => x.controls.recipientType.value === 'ReserveSlot'); }
  protected toggleMember(person: PersonDto, selected: boolean): void { const index = this.members.controls.findIndex((x) => x.controls.personId.value === person.id); if (selected && index < 0) this.members.push(this.memberGroup(person.id)); else if (!selected && index >= 0) { this.members.removeAt(index); for (let i = this.schedule.length - 1; i >= 0; i--) if (this.schedule.at(i).controls.personId.value === person.id) this.schedule.removeAt(i); } }
  protected addMemberPayout(): void { const first = this.members.at(0); if (first) this.schedule.push(this.scheduleGroup('Membership', first.controls.personId.value)); }
  protected addReserve(): void { this.schedule.push(this.scheduleGroup('ReserveSlot', '')); }
  protected removeSchedule(index: number): void { this.schedule.removeAt(index); }
  protected previous(): void { this.step.update((value) => Math.max(1, value - 1)); }
  protected next(): void { if (this.step() === 1) { this.basicForm.markAllAsTouched(); if (this.basicForm.invalid) return; } if (this.step() === 2 && this.members.length === 0) return; if (this.step() === 3 && this.members.invalid) return; this.step.update((value) => Math.min(6, value + 1)); }
  protected scheduleLooksBalanced(): boolean { return this.members.controls.every((member) => member.controls.kind.value === 'Advance' || this.schedule.controls.filter((entry) => entry.controls.recipientType.value === 'Membership' && entry.controls.personId.value === member.controls.personId.value).reduce((sum, entry) => sum + entry.controls.shareCount.value, 0) === member.controls.shareCount.value); }
  hasUnsavedChanges(): boolean { return !this.saving() && (this.basicForm.dirty || this.members.length > 0 || this.schedule.length > 0); }
  protected save(): void {
    if (this.saving() || this.basicForm.invalid || this.members.invalid || this.schedule.invalid) return;
    this.saving.set(true); this.error.set(''); const memberIds = new Map<string, string>(); const periodIds = new Map<number, string>(); let associationId = '';
    this.associationsApi.create(this.basicForm.getRawValue()).pipe(
      tap((association) => { associationId = association.id; association.periods.forEach((period) => periodIds.set(period.periodNumber, period.id)); }),
      switchMap(() => from(this.members.getRawValue()).pipe(concatMap((member) => this.associationsApi.addMember(associationId, member).pipe(tap((created) => memberIds.set(member.personId, created.id)))), toArray())),
      switchMap(() => from(this.schedule.getRawValue()).pipe(concatMap((entry) => this.associationsApi.addPayout(associationId, { periodId: periodIds.get(entry.periodNumber) ?? '', recipientType: entry.recipientType, membershipId: entry.recipientType === 'Membership' ? memberIds.get(entry.personId) ?? null : null, shareCount: entry.recipientType === 'ReserveSlot' ? 1 : entry.shareCount })), toArray())),
      finalize(() => this.saving.set(false)),
    ).subscribe({ next: () => { this.notifications.success('تم إنشاء الجمعية بنجاح.'); void this.router.navigate(['/owner/associations', associationId]); }, error: (error: unknown) => this.error.set(this.errors.message(error)) });
  }
  private memberGroup(personId: string): MemberGroup { return new FormGroup({ personId: new FormControl(personId, { nonNullable: true }), shareCount: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(.5), Validators.pattern(/^\d+(\.5)?$/)] }), kind: new FormControl<MembershipKind>('Paying', { nonNullable: true }) }); }
  private scheduleGroup(recipientType: PayoutRecipientType, personId: string): ScheduleGroup { return new FormGroup({ periodNumber: new FormControl(1, { nonNullable: true, validators: [Validators.required, Validators.min(1)] }), recipientType: new FormControl(recipientType, { nonNullable: true }), personId: new FormControl(personId, { nonNullable: true }), shareCount: new FormControl(recipientType === 'ReserveSlot' ? 1 : .5, { nonNullable: true, validators: [Validators.required, Validators.min(.5), Validators.pattern(/^\d+(\.5)?$/)] }) }); }
}
