import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { CreatePersonRequest, PagedResult, PersonDto, PersonProfileDto, UpdatePersonRequest } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class PeopleApiService {
  private readonly http = inject(HttpClient);
  private readonly url = `${environment.apiUrl}${API_ENDPOINTS.people}`;

  list(search = '', page = 1, pageSize = 20): Observable<PagedResult<PersonDto>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<PersonDto>>(this.url, { params });
  }

  create(request: CreatePersonRequest): Observable<PersonDto> {
    return this.http.post<PersonDto>(this.url, request);
  }

  profile(id: string): Observable<PersonProfileDto> {
    return this.http.get<PersonProfileDto>(`${this.url}/${id}`);
  }

  update(id: string, request: UpdatePersonRequest): Observable<PersonDto> {
    return this.http.put<PersonDto>(`${this.url}/${id}`, request);
  }

  deactivate(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
