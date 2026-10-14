import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { PagedResult, PaymentDto, PaymentPageDto, RecordPaymentRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PaymentsApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}${API_ENDPOINTS.payments}`;

  list(personId: string | null = null, page = 1, pageSize = 20): Observable<PagedResult<PaymentDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (personId) params = params.set('personId', personId);
    return this.http.get<PagedResult<PaymentDto>>(this.url, { params });
  }

  get(id: string): Observable<PaymentDto> { return this.http.get<PaymentDto>(`${this.url}/${id}`); }
  page(personId: string): Observable<PaymentPageDto> { return this.http.get<PaymentPageDto>(`${this.url}/page/${personId}`); }
  record(request: RecordPaymentRequest): Observable<PaymentDto> { return this.http.post<PaymentDto>(this.url, request); }
}
