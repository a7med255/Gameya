import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { API_ENDPOINTS } from '../constants/api-endpoints';
import { PersonProfileDto } from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ParticipantApiService {
  private readonly http = inject(HttpClient);
  me(): Observable<PersonProfileDto> { return this.http.get<PersonProfileDto>(`${environment.apiUrl}${API_ENDPOINTS.participantMe}`); }
}
