import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import {
  AdjustPaymentRequest,
  PagedResult,
  PaymentDto,
  PaymentFilters,
  PaymentListItemDto,
  PaymentPageDto,
  PaymentPlanningRequest,
  PaymentPreviewDto,
  RecordPaymentRequest
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PaymentsApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}${API_ENDPOINTS.payments}`;

  list(filters: PaymentFilters = {}, page = 1, pageSize = 20): Observable<PagedResult<PaymentListItemDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<PaymentListItemDto>>(this.url, { params });
  }

  get(id: string): Observable<PaymentDto> {
    return this.http.get<PaymentDto>(`${this.url}/${id}`);
  }

  page(personId: string): Observable<PaymentPageDto> {
    return this.http.get<PaymentPageDto>(`${this.url}/page/${personId}`);
  }

  preview(request: PaymentPlanningRequest): Observable<PaymentPreviewDto> {
    return this.http.post<PaymentPreviewDto>(`${this.url}/preview`, request);
  }

  record(request: RecordPaymentRequest): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(this.url, request);
  }

  adjust(id: string, request: AdjustPaymentRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(`${this.url}/${id}/adjustments`, request);
  }
}
