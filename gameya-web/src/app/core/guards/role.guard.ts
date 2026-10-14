import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { Role } from '../models/api.models';

export const roleGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  const allowed = (route.data['roles'] as readonly Role[] | undefined) ?? [];
  if (allowed.length === 0 || auth.hasRole(...allowed)) return true;
  return inject(Router).createUrlTree([auth.homeForCurrentUser()]);
};
