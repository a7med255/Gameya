import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { DashboardDto, MonthlyCollectionPersonDto, MonthlyPayoutPersonDto, PagedResult } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  get(year: number, month: number): Observable<DashboardDto> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get<DashboardDto>(`${environment.apiUrl}${API_ENDPOINTS.dashboard}`, { params });
  }
  collections(year: number, month: number, overdueOnly = false, page = 1, pageSize = 20): Observable<PagedResult<MonthlyCollectionPersonDto>> {
    const params = new HttpParams().set('year', year).set('month', month).set('overdueOnly', overdueOnly)
      .set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<MonthlyCollectionPersonDto>>(`${environment.apiUrl}${API_ENDPOINTS.dashboard}/monthly-collections`, { params });
  }
  payouts(year: number, month: number, page = 1, pageSize = 20): Observable<PagedResult<MonthlyPayoutPersonDto>> {
    const params = new HttpParams().set('year', year).set('month', month).set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<MonthlyPayoutPersonDto>>(`${environment.apiUrl}${API_ENDPOINTS.dashboard}/monthly-payouts`, { params });
  }
}
