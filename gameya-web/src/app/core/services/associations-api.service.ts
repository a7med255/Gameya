import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import {
  AddMemberRequest, AddPayoutRequest, AssociationDetailsDto, AssociationEditorDto, AssociationStatus, AssociationSummaryDto,
  CreatedId, CreateAssociationRequest, InvitePaymentUserRequest, MovePayoutRequest, PagedResult,
  PayoutDto, ReplaceMemberRequest,
  SaveAssociationConfigurationRequest,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class AssociationsApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}${API_ENDPOINTS.associations}`;

  list(search = '', status: AssociationStatus | '' = '', page = 1, pageSize = 20): Observable<PagedResult<AssociationSummaryDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (status) params = params.set('status', status);
    return this.http.get<PagedResult<AssociationSummaryDto>>(this.url, { params });
  }

  get(id: string): Observable<AssociationDetailsDto> { return this.http.get<AssociationDetailsDto>(`${this.url}/${id}`); }
  create(request: CreateAssociationRequest): Observable<AssociationDetailsDto> { return this.http.post<AssociationDetailsDto>(this.url, request); }
  createConfiguration(request: SaveAssociationConfigurationRequest): Observable<AssociationDetailsDto> {
    return this.http.post<AssociationDetailsDto>(`${this.url}/configuration`, request);
  }
  saveConfiguration(id: string, request: SaveAssociationConfigurationRequest): Observable<AssociationDetailsDto> {
    return this.http.put<AssociationDetailsDto>(`${this.url}/${id}/configuration`, request);
  }
  editor(id: string): Observable<AssociationEditorDto> { return this.http.get<AssociationEditorDto>(`${this.url}/${id}/editor`); }
  addMember(id: string, request: AddMemberRequest): Observable<CreatedId> { return this.http.post<CreatedId>(`${this.url}/${id}/members`, request); }
  changeShares(id: string, memberId: string, shareCount: number): Observable<void> {
    return this.http.patch<void>(`${this.url}/${id}/members/${memberId}/shares`, { shareCount });
  }
  removeMember(id: string, memberId: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}/members/${memberId}`);
  }
  start(id: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/start`, {}); }
  pause(id: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/pause`, {}); }
  cancel(id: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/cancel`, {}); }
  resume(id: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/resume`, {}); }
  complete(id: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/complete`, {}); }
  closePeriod(id: string, periodId: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/periods/${periodId}/close`, {}); }
  payouts(id: string): Observable<readonly PayoutDto[]> { return this.http.get<readonly PayoutDto[]>(`${this.url}/${id}/payouts`); }
  addPayout(id: string, request: AddPayoutRequest): Observable<CreatedId> { return this.http.post<CreatedId>(`${this.url}/${id}/payouts`, request); }
  movePayout(id: string, payoutId: string, request: MovePayoutRequest): Observable<void> { return this.http.put<void>(`${this.url}/${id}/payouts/${payoutId}`, request); }
  removePayout(id: string, payoutId: string, reason: string): Observable<void> {
    const params = reason.trim() ? new HttpParams().set('reason', reason.trim()) : undefined;
    return this.http.delete<void>(`${this.url}/${id}/payouts/${payoutId}`, { params });
  }
  completePayout(id: string, payoutId: string): Observable<void> { return this.http.post<void>(`${this.url}/${id}/payouts/${payoutId}/complete`, {}); }
  setReserveBeneficiary(id: string, payoutId: string, personId: string): Observable<void> {
    return this.http.put<void>(`${this.url}/${id}/payouts/${payoutId}/reserve-beneficiary`, { personId });
  }
  replaceMember(id: string, memberId: string, request: ReplaceMemberRequest): Observable<CreatedId> {
    return this.http.post<CreatedId>(`${this.url}/${id}/members/${memberId}/replace`, request);
  }
  invitePaymentUser(request: InvitePaymentUserRequest): Observable<CreatedId> {
    return this.http.post<CreatedId>(`${this.url}/invitations`, request);
  }
}
