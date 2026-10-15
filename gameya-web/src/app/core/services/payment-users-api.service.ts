import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import {
  CreatedId, InvitePaymentUserRequest, PagedResult, PaymentUserDto, PaymentUserInvitationDto,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PaymentUsersApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}${API_ENDPOINTS.paymentUsers}`;

  list(search = '', page = 1, pageSize = 20): Observable<PagedResult<PaymentUserDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<PaymentUserDto>>(this.url, { params });
  }

  invitations(search = '', page = 1, pageSize = 20): Observable<PagedResult<PaymentUserInvitationDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<PaymentUserInvitationDto>>(`${this.url}/invitations`, { params });
  }

  invite(request: InvitePaymentUserRequest): Observable<CreatedId> {
    return this.http.post<CreatedId>(`${this.url}/invitations`, request);
  }

  revokeInvitation(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/invitations/${id}`);
  }

  setAssociations(userId: string, associationIds: readonly string[]): Observable<void> {
    return this.http.put<void>(`${this.url}/${userId}/associations`, { associationIds });
  }

  deactivate(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${userId}`);
  }
}
