import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { SendReportResult } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}${API_ENDPOINTS.reports}`;

  download(year: number, month: number): Observable<Blob> {
    const params = new HttpParams().set('year', year).set('month', month);
    return this.http.get(this.url, { params, responseType: 'blob' });
  }

  send(year: number, month: number): Observable<SendReportResult> {
    return this.http.post<SendReportResult>(`${environment.apiUrl}${API_ENDPOINTS.sendMonthlyReport}`, { year, month });
  }

  downloadAssociation(associationId: string): Observable<Blob> {
    return this.http.get(`${environment.apiUrl}/reports/associations/${associationId}`, { responseType: 'blob' });
  }

  sendAssociation(associationId: string): Observable<void> {
    return this.http.post<void>(`${environment.apiUrl}/reports/associations/${associationId}/send`, {});
  }
}
