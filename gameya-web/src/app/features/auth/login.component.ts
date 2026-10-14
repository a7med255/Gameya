import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AuthFrameComponent } from './auth-frame.component';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AuthFrameComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-frame>
      <div class="brand-mark" style="margin-bottom:2rem"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
      <h1 class="page-title">أهلًا بيك</h1><p class="page-subtitle">سجّل دخولك علشان تكمّل إدارة جمعياتك.</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      <form class="stack" style="margin-top:1.5rem" [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <div class="field">
          <label for="email">البريد الإلكتروني</label>
          <input class="control" id="email" type="email" dir="ltr" autocomplete="email" formControlName="email" [attr.aria-invalid]="invalid('email')">
          @if (invalid('email')) { <span class="field__error">اكتب بريد إلكتروني صحيح.</span> }
        </div>
        <div class="field">
          <label for="password">كلمة المرور</label>
          <input class="control" id="password" type="password" dir="ltr" autocomplete="current-password" formControlName="password" [attr.aria-invalid]="invalid('password')">
          @if (invalid('password')) { <span class="field__error">كلمة المرور مطلوبة.</span> }
        </div>
        <a class="muted" routerLink="/auth/forgot-password">نسيت كلمة المرور؟</a>
        <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">{{ loading() ? 'جاري الدخول…' : 'تسجيل الدخول' }}</button>
      </form>
      <p class="muted" style="text-align:center">لسه معندكش حساب؟ <a class="success-text" routerLink="/auth/register">اعمل حساب جديد</a></p>
      <p style="text-align:center"><a class="muted" routerLink="/participant/login">دخول المشترك برقم الموبايل</a></p>
    </app-auth-frame>
  `,
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly errors = inject(ApiErrorService);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    password: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });

  protected invalid(name: 'email' | 'password'): boolean {
    const control = this.form.controls[name];
    return control.invalid && (control.touched || this.form.touched);
  }

  protected submit(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid || this.loading()) return;
    this.loading.set(true); this.error.set('');
    this.auth.login(this.form.getRawValue()).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: () => {
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        void this.router.navigateByUrl(returnUrl?.startsWith('/') ? returnUrl : this.auth.homeForCurrentUser());
      },
      error: (error: unknown) => this.error.set(this.errors.message(error)),
    });
  }
}
