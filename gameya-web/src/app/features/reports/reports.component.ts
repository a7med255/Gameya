import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize } from 'rxjs';
import { ApiErrorService } from '../../core/services/api-error.service';
import { ConfirmService } from '../../core/services/confirm.service';
import { NotificationService } from '../../core/services/notification.service';
import { ReportsApiService } from '../../core/services/reports-api.service';

@Component({
  selector: 'app-reports', standalone: true, imports: [ReactiveFormsModule], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page reports-page"><header class="page-header"><div><h1 class="page-title">التقارير</h1><p class="page-subtitle">نزّل التقرير الشهري من الخادم أو ابعته على البريد الإلكتروني.</p></div></header>
      <section class="content-grid"><form class="card stack" [formGroup]="form"><div><h2 class="section-title">اختار الشهر</h2><p class="muted">التقرير المالي بيتولد من البيانات المعتمدة في النظام.</p></div><div class="form-grid"><div class="field"><label for="month">الشهر</label><select class="control" id="month" formControlName="month">@for (month of months; track month.value) { <option [value]="month.value">{{ month.label }}</option> }</select></div><div class="field"><label for="year">السنة</label><input class="control" id="year" type="number" min="2000" max="2200" formControlName="year"></div></div><div class="inline"><button class="btn btn--primary" type="button" [disabled]="loading()" (click)="download()">تنزيل PDF</button><button class="btn btn--secondary" type="button" [disabled]="loading()" (click)="send()">إرسال بالبريد</button></div></form>
        <aside class="card"><h2 class="section-title">محتوى التقرير</h2><ul class="report-list"><li>ملخص الجمعيات والمدفوعات</li><li>الفترات المستحقة والمتأخرة</li><li>بيانات القبض والتسويات المتاحة</li></ul><p class="alert alert--info">ملف PDF والحسابات بيتولدوا بالكامل على الخادم، مش في المتصفح.</p></aside></section>
      <section class="card" style="margin-top:1rem"><h2 class="section-title">سجل التقارير</h2><p class="muted">إرسال التقرير بيتم فورًا عند الطلب. سجل تنزيلات المستخدم غير محفوظ على الجهاز حفاظًا على الخصوصية.</p>@if (lastJobId()) { <div class="alert alert--info">تم إرسال التقرير. رقم التنفيذ: <span class="code">{{ lastJobId() }}</span></div> }</section>
    </div>
  `,
  styles: [`.reports-page{max-width:1100px}.report-list{padding-inline-start:1.3rem;display:grid;gap:.65rem}`],
})
export class ReportsComponent {
  private readonly api = inject(ReportsApiService); private readonly notifications = inject(NotificationService); private readonly errors = inject(ApiErrorService); private readonly confirm = inject(ConfirmService);
  protected readonly loading = signal(false); protected readonly lastJobId = signal(''); protected readonly months = Array.from({ length: 12 }, (_, index) => ({ value: index + 1, label: new Intl.DateTimeFormat('ar-EG', { month: 'long' }).format(new Date(2026, index, 1)) }));
  protected readonly form = new FormGroup({ year: new FormControl(new Date().getFullYear(), { nonNullable: true, validators: [Validators.required, Validators.min(2000), Validators.max(2200)] }), month: new FormControl(new Date().getMonth() + 1, { nonNullable: true, validators: [Validators.required, Validators.min(1), Validators.max(12)] }) });
  protected download(): void { if (this.form.invalid || this.loading()) return; this.loading.set(true); const { year, month } = this.form.getRawValue(); this.api.download(year, month).pipe(finalize(() => this.loading.set(false))).subscribe({ next: (blob) => { const url = URL.createObjectURL(blob); const anchor = document.createElement('a'); anchor.href = url; anchor.download = `تقرير-الجمعية-${year}-${String(month).padStart(2, '0')}.pdf`; anchor.click(); URL.revokeObjectURL(url); this.notifications.success('تم تنزيل التقرير.'); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
  protected async send(): Promise<void> { if (this.form.invalid || this.loading()) return; const { year, month } = this.form.getRawValue(); if (!await this.confirm.ask({ title: 'إرسال التقرير', message: `هيتم إرسال تقرير ${this.months[month - 1]?.label} ${year} على بريدك فورًا.`, confirmLabel: 'إرسال' })) return; this.loading.set(true); this.api.send(year, month).pipe(finalize(() => this.loading.set(false))).subscribe({ next: (result) => { this.lastJobId.set(result.executionId); this.notifications.success('تم إرسال التقرير على البريد.'); }, error: (error: unknown) => this.notifications.error(this.errors.message(error)) }); }
}
