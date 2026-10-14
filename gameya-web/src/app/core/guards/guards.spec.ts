import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AssociationContextService } from '../services/association-context.service';
import { associationAccessGuard } from './association-access.guard';
import { authGuard } from './auth.guard';
import { roleGuard } from './role.guard';

const state = { url: '/owner/people' } as RouterStateSnapshot;

describe('route guards', () => {
  const tree = {} as UrlTree;
  const router = { createUrlTree: vi.fn(() => tree) };

  it('redirects an unauthenticated visitor', () => {
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: { isAuthenticated: () => false } }, { provide: Router, useValue: router }] });
    const result = TestBed.runInInjectionContext(() => authGuard({} as ActivatedRouteSnapshot, state));
    expect(result).toBe(tree);
  });

  it('keeps PaymentUser out of owner-only routes', () => {
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: { hasRole: () => false, homeForCurrentUser: () => '/payment/pay' } }, { provide: Router, useValue: router }] });
    const route = { data: { roles: ['Admin', 'Owner'] } } as unknown as ActivatedRouteSnapshot;
    const result = TestBed.runInInjectionContext(() => roleGuard(route, state));
    expect(result).toBe(tree);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/payment/pay']);
  });

  it('allows only a PaymentUser association already returned by an accessible API response', () => {
    const context = new AssociationContextService(); context.setAccessible(['allowed-id']);
    TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: { hasRole: () => false } }, { provide: AssociationContextService, useValue: context }, { provide: Router, useValue: router }] });
    const route = { paramMap: { get: () => 'allowed-id' } } as unknown as ActivatedRouteSnapshot;
    expect(TestBed.runInInjectionContext(() => associationAccessGuard(route, state))).toBe(true);
  });
});
