import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../auth/auth.service';
import { AssociationContextService } from '../services/association-context.service';

export const associationAccessGuard: CanActivateFn = (route) => {
  const auth = inject(AuthService);
  if (auth.hasRole('Admin', 'Owner')) return true;
  const id = route.paramMap.get('id');
  return id && inject(AssociationContextService).canAccess(id)
    ? true
    : inject(Router).createUrlTree(['/not-found']);
};
