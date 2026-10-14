import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { DashboardDto } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  private readonly http = inject(HttpClient);
  get(): Observable<DashboardDto> { return this.http.get<DashboardDto>(`${environment.apiUrl}${API_ENDPOINTS.dashboard}`); }
}
