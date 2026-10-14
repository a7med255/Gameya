import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { ApiErrorService } from '../../core/services/api-error.service';
import { AuthFrameComponent } from './auth-frame.component';

@Component({
  selector: 'app-participant-login', standalone: true, imports: [ReactiveFormsModule, RouterLink, AuthFrameComponent], changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-frame>
      <div class="brand-mark" style="margin-bottom:2rem"><span class="brand-mark__logo">ج</span><span>جمعية</span></div>
      <h1 class="page-title">دخول المشترك</h1><p class="page-subtitle">شوف جمعياتك ومدفوعاتك ومواعيد القبض.</p>
      @if (error()) { <p class="alert alert--error" role="alert">{{ error() }}</p> }
      <form class="stack" style="margin-top:1.5rem" [formGroup]="form" (ngSubmit)="submit()">
        <div class="field"><label for="phone">رقم الموبايل</label><input class="control" id="phone" type="tel" dir="ltr" inputmode="tel" autocomplete="tel" formControlName="phoneNumber">@if (invalid('phoneNumber')) { <span class="field__error">رقم الموبايل مطلوب.</span> }</div>
        <div class="field"><label for="code">كود المشترك</label><input class="control code" id="code" maxlength="8" autocomplete="one-time-code" formControlName="subscriberCode" (input)="normalizeCode()">@if (invalid('subscriberCode')) { <span class="field__error">الكود مكوّن من 8 حروف أو أرقام.</span> }</div>
        <button class="btn btn--primary btn--block" type="submit" [disabled]="loading()">{{ loading() ? 'جاري الدخول…' : 'عرض بياناتي' }}</button>
      </form>
      <p style="text-align:center"><a class="muted" routerLink="/auth/login">دخول مالك الجمعية أو مسجل المدفوعات</a></p>
    </app-auth-frame>
  `,
})
export class ParticipantLoginComponent {
  private readonly auth = inject(AuthService); private readonly router = inject(Router); private readonly errors = inject(ApiErrorService);
  protected readonly loading = signal(false); protected readonly error = signal('');
  protected readonly form = new FormGroup({
    phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(30)] }),
    subscriberCode: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^[A-Za-z0-9]{8}$/)] }),
  });
  protected invalid(name: keyof typeof this.form.controls): boolean { const control = this.form.controls[name]; return control.invalid && control.touched; }
  protected normalizeCode(): void { this.form.controls.subscriberCode.setValue(this.form.controls.subscriberCode.value.toUpperCase(), { emitEvent: false }); }
  protected submit(): void {
    this.form.markAllAsTouched(); if (this.form.invalid || this.loading()) return;
    this.loading.set(true); this.error.set('');
    this.auth.participantLogin(this.form.getRawValue()).pipe(finalize(() => this.loading.set(false))).subscribe({ next: () => void this.router.navigate(['/participant']), error: (error: unknown) => this.error.set(this.errors.message(error)) });
  }
}
