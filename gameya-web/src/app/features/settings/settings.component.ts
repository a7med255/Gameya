import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { AssociationSummaryDto } from '../../core/models/api.models';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AssociationsApiService } from '../../core/services/associations-api.service';
import { NotificationService } from '../../core/services/notification.service';

@Component({
  selector: 'app-settings', standalone: true, imports: [ReactiveFormsModule], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page settings-page"><header class="page-header"><div><h1 class="page-title">الإعدادات</h1><p class="page-subtitle">إدارة فريق تسجيل المدفوعات وصلاحيات الجمعيات.</p></div></header>
      <section class="card"><h2 class="section-title">دعوة مسجل مدفوعات</h2><p class="muted">هيتبعت له رابط صالح لمدة 7 أيام. مش هيشوف غير الجمعيات اللي تختارها.</p><form class="stack" [formGroup]="form" (ngSubmit)="invite()"><div class="field"><label for="inviteEmail">البريد الإلكتروني</label><input class="control" id="inviteEmail" type="email" dir="ltr" autocomplete="email" formControlName="email">@if (form.controls.email.invalid && form.controls.email.touched) { <span class="field__error">اكتب بريد إلكتروني صحيح.</span> }</div><fieldset class="association-choices"><legend>الجمعيات المسموح بها</legend>@for (association of associations(); track association.id) { <label><input #accessCheck type="checkbox" [checked]="selectedIds().includes(association.id)" (change)="toggle(association.id, accessCheck.checked)"><span>{{ association.name }}</span></label> } @if (associations().length === 0) { <p class="muted">أنشئ جمعية الأول علشان تقدر تبعت دعوة.</p> }</fieldset><button class="btn btn--primary" type="submit" [disabled]="saving() || selectedIds().length === 0">{{ saving() ? 'جاري إرسال الدعوة…' : 'إرسال الدعوة' }}</button></form></section>
      <section class="card" style="margin-top:1rem"><h2 class="section-title">الأمان</h2><p class="muted">الصلاحيات الظاهرة هنا لتحسين التجربة فقط. الخادم بيتحقق من كل طلب ومبيكشفش بيانات جمعية غير مصرح بها.</p></section>
    </div>
  `,
  styles: [`.settings-page{max-width:850px}.association-choices{border:1px solid var(--line);border-radius:.8rem;padding:1rem;display:grid;gap:.7rem}.association-choices legend{font-weight:800;padding-inline:.4rem}.association-choices label{display:flex;align-items:center;gap:.6rem;min-height:2.5rem}`],
})
export class SettingsComponent {
  private readonly api = inject(AssociationsApiService); private readonly notifications = inject(NotificationService); private readonly errors = inject(ApiErrorService);
  protected readonly associations = signal<readonly AssociationSummaryDto[]>([]); protected readonly selectedIds = signal<readonly string[]>([]); protected readonly saving = signal(false); protected readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }) });
  constructor() { this.api.list('', '', 1, 100).subscribe({ next: (result) => this.associations.set(result.items) }); }
  protected toggle(id: string, checked: boolean): void { this.selectedIds.update((ids) => checked ? [...ids, id] : ids.filter((value) => value !== id)); }
  protected invite(): void { this.form.markAllAsTouched(); if (this.form.invalid || this.selectedIds().length === 0 || this.saving()) return; this.saving.set(true); this.api.invitePaymentUser({ email: this.form.controls.email.value, associationIds: this.selectedIds() }).pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => { this.notifications.success('تم إرسال الدعوة بنجاح.'); this.form.reset(); this.selectedIds.set([]); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
}
