import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AuthApiService } from '../../core/services/auth-api.service';
import { AuthFrameComponent } from './auth-frame.component';

@Component({
  selector: 'app-password-reset', standalone: true,
  imports: [ReactiveFormsModule, RouterLink, AuthFrameComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-frame>
      <h1 class="page-title">{{ isReset ? 'اختيار كلمة مرور جديدة' : 'استعادة كلمة المرور' }}</h1>
      <p class="page-subtitle">{{ isReset ? 'اكتب كلمة قوية لا تقل عن 10 أحرف.' : 'سنرسل رابطًا آمنًا إذا كان البريد مسجلًا.' }}</p>
      @if (message()) { <p class="alert alert--info" role="status">{{ message() }}</p> }
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      <form class="stack" [formGroup]="form" (ngSubmit)="submit()">
        @if (!isReset) {
          <div class="field"><label for="resetEmail">البريد الإلكتروني</label><input id="resetEmail" class="control" type="email" dir="ltr" formControlName="email"></div>
        } @else {
          <div class="field"><label for="newPassword">كلمة المرور الجديدة</label><input id="newPassword" class="control" type="password" dir="ltr" autocomplete="new-password" formControlName="newPassword"></div>
        }
        <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">{{ loading() ? 'جاري التنفيذ…' : (isReset ? 'حفظ كلمة المرور' : 'إرسال الرابط') }}</button>
      </form>
      <p style="text-align:center"><a routerLink="/auth/login">العودة لتسجيل الدخول</a></p>
    </app-auth-frame>
  `,
})
export class PasswordResetComponent {
  private readonly api = inject(AuthApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly errors = inject(ApiErrorService);
  protected readonly userId = this.route.snapshot.queryParamMap.get('userId') ?? '';
  protected readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';
  protected readonly isReset = !!this.userId && !!this.token;
  protected readonly loading = signal(false);
  protected readonly message = signal('');
  protected readonly error = signal('');
  protected readonly form = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    newPassword: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(10)] }),
  });
  protected submit(): void {
    const control = this.isReset ? this.form.controls.newPassword : this.form.controls.email;
    control.markAsTouched();
    if (control.invalid || this.loading()) return;
    this.loading.set(true); this.error.set(''); this.message.set('');
    const operation = this.isReset
      ? this.api.resetPassword(this.userId, this.token, this.form.controls.newPassword.value)
      : this.api.requestPasswordReset(this.form.controls.email.value);
    operation.pipe(finalize(() => this.loading.set(false))).subscribe({
      next: () => this.message.set(this.isReset ? 'تم تغيير كلمة المرور. يمكنك تسجيل الدخول الآن.' : 'إذا كان البريد مسجلًا فستصلك رسالة الاستعادة.'),
      error: (error: unknown) => this.error.set(this.errors.message(error)),
    });
  }
}
