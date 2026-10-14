import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, finalize, firstValueFrom, map, Observable, of, shareReplay, tap, throwError } from 'rxjs';
import { ROLE_HOME, ROLES } from '../constants/app.constants';
import { AuthTokensDto, CurrentUser, LoginRequest, ParticipantLoginRequest, RegisterRequest, Role } from '../models/api.models';
import { AuthApiService } from '../services/auth-api.service';

const REFRESH_TOKEN_KEY = 'gameya.refresh-token';
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const NAME_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const NAME_ID_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const EMAIL_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';

type JwtPayload = Readonly<Record<string, unknown>> & { exp?: number };

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApiService);
  private readonly router = inject(Router);
  private readonly accessTokenState = signal<string | null>(null);
  private readonly userState = signal<CurrentUser | null>(null);
  private refreshRequest: Observable<string> | null = null;

  readonly accessToken = this.accessTokenState.asReadonly();
  readonly currentUser = this.userState.asReadonly();
  readonly isAuthenticated = computed(() => this.userState() !== null);
  readonly roles = computed(() => this.userState()?.roles ?? []);
  readonly isParticipant = computed(() => this.hasRole(ROLES.participant));

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.api.login(request).pipe(map((tokens) => this.acceptTokens(tokens)));
  }

  participantLogin(request: ParticipantLoginRequest): Observable<CurrentUser> {
    return this.api.participantLogin(request).pipe(map((tokens) => this.acceptTokens(tokens)));
  }

  register(request: RegisterRequest) { return this.api.register(request); }

  async restore(): Promise<void> {
    if (!this.getRefreshToken()) return;
    await firstValueFrom(this.refreshSession().pipe(map(() => undefined), catchError(() => of(undefined))));
  }

  refreshSession(): Observable<string> {
    if (this.refreshRequest) return this.refreshRequest;
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) return throwError(() => new Error('Missing refresh token'));

    this.refreshRequest = this.api.refresh(refreshToken).pipe(
      map((tokens) => {
        this.acceptTokens(tokens);
        return tokens.accessToken;
      }),
      catchError((error: unknown) => {
        this.clearSession();
        return throwError(() => error);
      }),
      finalize(() => { this.refreshRequest = null; }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.refreshRequest;
  }

  logout(): void {
    const refreshToken = this.getRefreshToken();
    const request = refreshToken ? this.api.logout(refreshToken) : of(undefined);
    request.pipe(finalize(() => {
      this.clearSession();
      void this.router.navigate(['/auth/login']);
    })).subscribe({ error: () => undefined });
  }

  clearSession(): void {
    this.accessTokenState.set(null);
    this.userState.set(null);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
  }

  hasRole(...roles: readonly Role[]): boolean {
    return roles.some((role) => this.roles().includes(role));
  }

  homeForCurrentUser(): string {
    const role = this.roles()[0];
    return role ? ROLE_HOME[role] : '/auth/login';
  }

  private acceptTokens(tokens: AuthTokensDto): CurrentUser {
    const payload = decodeJwt(tokens.accessToken);
    const user = payloadToUser(payload);
    this.accessTokenState.set(tokens.accessToken);
    this.userState.set(user);
    sessionStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
    return user;
  }

  private getRefreshToken(): string | null { return sessionStorage.getItem(REFRESH_TOKEN_KEY); }
}

export function decodeJwt(token: string): JwtPayload {
  const encoded = token.split('.')[1];
  if (!encoded) throw new Error('Invalid access token');
  const base64 = encoded.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(encoded.length / 4) * 4, '=');
  return JSON.parse(decodeURIComponent(Array.from(atob(base64))
    .map((character) => `%${character.charCodeAt(0).toString(16).padStart(2, '0')}`).join(''))) as JwtPayload;
}

export function payloadToUser(payload: JwtPayload): CurrentUser {
  const rawRoles = payload['role'] ?? payload[ROLE_CLAIM] ?? [];
  const roleValues = Array.isArray(rawRoles) ? rawRoles : [rawRoles];
  const allowedRoles: readonly Role[] = ['Admin', 'Owner', 'PaymentUser', 'Participant'];
  const roles = roleValues.filter((value): value is Role => typeof value === 'string' && allowedRoles.includes(value as Role));
  const read = (...keys: readonly string[]): string | null => {
    for (const key of keys) {
      const value = payload[key];
      if (typeof value === 'string') return value;
    }
    return null;
  };
  return {
    subject: read('nameid', 'sub', NAME_ID_CLAIM) ?? '',
    displayName: read('unique_name', 'name', NAME_CLAIM, 'phone') ?? 'مستخدم جمعية',
    email: read('email', EMAIL_CLAIM),
    personId: read('person_id'),
    roles,
  };
}
