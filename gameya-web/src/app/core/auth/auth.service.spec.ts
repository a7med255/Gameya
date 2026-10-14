import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { Subject } from 'rxjs';
import { AuthTokensDto } from '../models/api.models';
import { AuthApiService } from '../services/auth-api.service';
import { AuthService, decodeJwt, payloadToUser } from './auth.service';

function token(payload: object): string {
  return `header.${btoa(JSON.stringify(payload)).replace(/=/g, '').replace(/\+/g, '-').replace(/\//g, '_')}.signature`;
}

describe('AuthService', () => {
  afterEach(() => sessionStorage.clear());

  it('reads participant identity and role from the backend JWT shape', () => {
    const payload = decodeJwt(token({ nameid: 'participant:person-1', person_id: 'person-1', role: 'Participant', phone: '01000000000' }));
    expect(payloadToUser(payload)).toEqual({ subject: 'participant:person-1', displayName: '01000000000', email: null, personId: 'person-1', roles: ['Participant'] });
  });

  it('normalizes a single or multiple role claim', () => {
    expect(payloadToUser({ nameid: '1', role: 'Owner' }).roles).toEqual(['Owner']);
    expect(payloadToUser({ nameid: '1', role: ['Admin', 'Owner'] }).roles).toEqual(['Admin', 'Owner']);
  });

  it('shares one refresh request between simultaneous callers', () => {
    const response = new Subject<AuthTokensDto>();
    const api = { refresh: vi.fn(() => response.asObservable()) };
    TestBed.configureTestingModule({ providers: [AuthService, { provide: AuthApiService, useValue: api }, { provide: Router, useValue: { navigate: vi.fn() } }] });
    sessionStorage.setItem('gameya.refresh-token', 'refresh-token');
    const service = TestBed.inject(AuthService);
    const first = service.refreshSession();
    const second = service.refreshSession();
    expect(first).toBe(second);
    expect(api.refresh).toHaveBeenCalledTimes(1);
  });
});
