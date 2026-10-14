import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import {
  AuthTokensDto, LoginRequest, ParticipantLoginRequest, RegisterRequest, RegistrationResultDto,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiUrl;

  login(request: LoginRequest): Observable<AuthTokensDto> {
    return this.http.post<AuthTokensDto>(`${this.base}${API_ENDPOINTS.authLogin}`, request);
  }

  participantLogin(request: ParticipantLoginRequest): Observable<AuthTokensDto> {
    return this.http.post<AuthTokensDto>(`${this.base}${API_ENDPOINTS.participantLogin}`, request);
  }

  register(request: RegisterRequest): Observable<RegistrationResultDto> {
    return this.http.post<RegistrationResultDto>(`${this.base}${API_ENDPOINTS.authRegister}`, request);
  }

  refresh(refreshToken: string): Observable<AuthTokensDto> {
    return this.http.post<AuthTokensDto>(`${this.base}${API_ENDPOINTS.refresh}`, { refreshToken });
  }

  logout(refreshToken: string): Observable<void> {
    return this.http.post<void>(`${this.base}${API_ENDPOINTS.logout}`, { refreshToken });
  }

  resendConfirmation(email: string): Observable<void> {
    return this.http.post<void>(`${this.base}${API_ENDPOINTS.resendConfirmation}`, { email });
  }

  confirmEmail(userId: string, token: string): Observable<void> {
    const params = new HttpParams().set('userId', userId).set('token', token);
    return this.http.get<void>(`${this.base}${API_ENDPOINTS.confirmEmail}`, { params });
  }

  acceptInvitation(token: string, password: string): Observable<void> {
    return this.http.post<void>(`${this.base}${API_ENDPOINTS.acceptInvitation}`, { token, password });
  }
}
