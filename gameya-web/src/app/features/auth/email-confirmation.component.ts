import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AuthApiService } from '../../core/services/auth-api.service';
import { AuthFrameComponent } from './auth-frame.component';

@Component({
  selector: 'app-email-confirmation', standalone: true, imports: [ReactiveFormsModule, RouterLink, AuthFrameComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-frame>
      <div class="brand-mark" style="margin-bottom:2rem"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
      @if (confirming()) { <section class="state"><div class="state__content"><div class="skeleton skeleton-line"></div><h1>جاري تأكيد البريد…</h1></div></section> }
      @else if (confirmed()) { <section class="state"><div class="state__content"><div class="state__icon">✓</div><h1>تم تأكيد البريد</h1><p class="muted">حسابك جاهز للاستخدام.</p><a class="btn btn--primary" routerLink="/auth/login">تسجيل الدخول</a></div></section> }
      @else {
        <h1 class="page-title">إعادة إرسال رسالة التأكيد</h1><p class="page-subtitle">اكتب بريدك وهانبعتلك رابط جديد لو الحساب محتاج تأكيد.</p>@if (message()) { <p class="alert" [class.alert--error]="hasError()" [class.alert--info]="!hasError()">{{ message() }}</p> }<form class="stack" style="margin-top:1.5rem" [formGroup]="form" (ngSubmit)="resend()"><div class="field"><label for="confirmationEmail">البريد الإلكتروني</label><input class="control" id="confirmationEmail" type="email" dir="ltr" formControlName="email"></div><button class="btn btn--primary" type="submit" [disabled]="sending()">{{ sending() ? 'جاري الإرسال…' : 'إرسال رابط جديد' }}</button></form><p><a class="muted" routerLink="/auth/login">رجوع لتسجيل الدخول</a></p>
      }
    </app-auth-frame>
  `,
})
export class EmailConfirmationComponent {
  private readonly api = inject(AuthApiService); private readonly errors = inject(ApiErrorService); private readonly route = inject(ActivatedRoute);
  protected readonly confirming = signal(false); protected readonly confirmed = signal(false); protected readonly sending = signal(false); protected readonly message = signal(''); protected readonly hasError = signal(false);
  protected readonly form = new FormGroup({ email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }) });
  constructor() { const userId = this.route.snapshot.queryParamMap.get('userId'); const token = this.route.snapshot.queryParamMap.get('token'); if (userId && token) { this.confirming.set(true); this.api.confirmEmail(userId, token).pipe(finalize(() => this.confirming.set(false))).subscribe({ next: () => this.confirmed.set(true), error: (error: unknown) => { this.hasError.set(true); this.message.set(this.errors.message(error)); } }); } }
  protected resend(): void { this.form.markAllAsTouched(); if (this.form.invalid || this.sending()) return; this.sending.set(true); this.hasError.set(false); this.api.resendConfirmation(this.form.controls.email.value).pipe(finalize(() => this.sending.set(false))).subscribe({ next: () => this.message.set('لو الحساب محتاج تأكيد، هتوصلك رسالة جديدة.'), error: (error: unknown) => { this.hasError.set(true); this.message.set(this.errors.message(error)); } }); }
}
