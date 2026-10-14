import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../core/auth/auth.service';

@Component({ selector: 'app-not-found', standalone: true, imports: [RouterLink], changeDetection: ChangeDetectionStrategy.OnPush, template: `<main class="state" style="min-height:100dvh"><div class="state__content"><div class="state__icon">؟</div><h1>الصفحة مش موجودة</h1><p class="muted">الرابط غير صحيح أو البيانات مش متاحة لحسابك.</p><a class="btn btn--primary" [routerLink]="home">الرجوع للرئيسية</a></div></main>` })
export class NotFoundComponent { private readonly auth = inject(AuthService); protected readonly home = this.auth.isAuthenticated() ? this.auth.homeForCurrentUser() : '/auth/login'; }
