import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AuthFrameComponent } from './auth-frame.component';

@Component({
  selector: 'app-register', standalone: true, imports: [ReactiveFormsModule, RouterLink, AuthFrameComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-frame>
      <div class="brand-mark" style="margin-bottom:2rem"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
      @if (done()) {
        <section class="state">
          <div class="state__content"><div class="state__icon">✓</div><h1>راجع بريدك الإلكتروني</h1><p class="muted">بعتنالك رسالة تأكيد. افتح الرابط الموجود فيها وبعدها سجّل دخولك.</p><a class="btn btn--primary" routerLink="/auth/login">الذهاب لتسجيل الدخول</a></div>
        </section>
      } @else {
        <h1 class="page-title">ابدأ مع جمعية</h1><p class="page-subtitle">أنشئ حساب مالك في دقائق.</p>
        @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
        <form class="stack" style="margin-top:1.5rem" [formGroup]="form" (ngSubmit)="submit()">
          <div class="field"><label for="name">الاسم</label><input class="control" id="name" autocomplete="name" formControlName="name">@if (invalid('name')) { <span class="field__error">الاسم مطلوب.</span> }</div>
          <div class="field"><label for="email">البريد الإلكتروني</label><input class="control" id="email" type="email" dir="ltr" autocomplete="email" formControlName="email">@if (invalid('email')) { <span class="field__error">اكتب بريد إلكتروني صحيح.</span> }</div>
          <div class="field"><label for="password">كلمة المرور</label><input class="control" id="password" type="password" dir="ltr" autocomplete="new-password" formControlName="password"><span class="field__hint">10 حروف على الأقل، مع حرف كبير وصغير ورقم ورمز.</span>@if (invalid('password')) { <span class="field__error">كلمة المرور لازم تكون 10 حروف على الأقل.</span> }</div>
          <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">{{ loading() ? 'جاري إنشاء الحساب…' : 'إنشاء الحساب' }}</button>
        </form>
        <p class="muted" style="text-align:center">عندك حساب؟ <a class="success-text" routerLink="/auth/login">سجّل دخولك</a></p>
      }
    </app-auth-frame>
  `,
})
export class RegisterComponent {
  private readonly auth = inject(AuthService); private readonly errors = inject(ApiErrorService);
  protected readonly loading = signal(false); protected readonly done = signal(false); protected readonly error = signal('');
  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(200)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(10)] }),
  });
  protected invalid(name: keyof typeof this.form.controls): boolean { const control = this.form.controls[name]; return control.invalid && control.touched; }
  protected submit(): void {
    this.form.markAllAsTouched(); if (this.form.invalid || this.loading()) return;
    this.loading.set(true); this.error.set('');
    this.auth.register(this.form.getRawValue()).pipe(finalize(() => this.loading.set(false))).subscribe({ next: () => this.done.set(true), error: (error: unknown) => this.error.set(this.errors.message(error)) });
  }
}
