import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthApiService } from '../../core/services/auth-api.service';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AuthFrameComponent } from './auth-frame.component';

@Component({
  selector: 'app-accept-invitation', standalone: true, imports: [ReactiveFormsModule, RouterLink, AuthFrameComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-frame>
      <div class="brand-mark" style="margin-bottom:2rem"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
      @if (done()) {
        <section class="state"><div class="state__content"><div class="state__icon">✓</div><h1>تم تفعيل الحساب بنجاح.</h1><p class="muted">سيتم تحويلك إلى صفحة تسجيل الدخول.</p><a class="btn btn--primary" routerLink="/auth/login">تسجيل الدخول</a></div></section>
      } @else {
        <h1 class="page-title">فعّل دعوتك</h1><p class="page-subtitle">اختار كلمة مرور آمنة لحساب تسجيل المدفوعات.</p>
        @if (!token) { <p class="alert alert--error">رابط الدعوة غير مكتمل.</p> }
        @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
        <form class="stack" style="margin-top:1.5rem" [formGroup]="form" (ngSubmit)="submit()">
          <div class="field"><label for="password">كلمة المرور</label><input class="control" id="password" type="password" dir="ltr" autocomplete="new-password" formControlName="password"><span class="field__hint">10 حروف على الأقل، وحرف كبير وصغير ورقم ورمز.</span></div>
          <button class="btn btn--primary btn--block" type="submit" [disabled]="loading() || !token">{{ loading() ? 'جاري التفعيل…' : 'تفعيل الحساب' }}</button>
        </form>
      }
    </app-auth-frame>
  `,
})
export class AcceptInvitationComponent {
  private readonly api = inject(AuthApiService); private readonly errors = inject(ApiErrorService); private readonly router = inject(Router);
  protected readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token');
  protected readonly form = new FormGroup({ password: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(10), Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/)] }) });
  protected readonly loading = signal(false); protected readonly done = signal(false); protected readonly error = signal('');
  protected submit(): void {
    this.form.markAllAsTouched(); if (!this.token || this.form.invalid || this.loading()) return;
    this.loading.set(true); this.error.set(''); this.api.acceptInvitation(this.token, this.form.controls.password.value).pipe(finalize(() => this.loading.set(false))).subscribe({ next: () => { this.done.set(true); window.setTimeout(() => void this.router.navigate(['/auth/login']), 1500); }, error: (error: unknown) => this.error.set(this.errors.message(error)) });
  }
}
