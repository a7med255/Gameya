import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { ApiErrorService } from '../services/api-error.service';
import { NotificationService } from '../services/notification.service';

export const apiInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const errors = inject(ApiErrorService);
  const notifications = inject(NotificationService);
  const isRefresh = request.url.endsWith('/auth/refresh');
  const token = auth.accessToken();
  const headers: Record<string, string> = {
    'Accept-Language': 'ar-EG',
    'X-Correlation-ID': crypto.randomUUID(),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  const authenticatedRequest = request.clone({ setHeaders: headers });

  return next(authenticatedRequest).pipe(catchError((error: unknown) => {
    if (error instanceof HttpErrorResponse && error.status === 401 && token && !isRefresh) {
      return auth.refreshSession().pipe(
        switchMap((newToken) => next(request.clone({ setHeaders: { ...headers, Authorization: `Bearer ${newToken}` } }))),
        catchError((refreshError: unknown) => {
          auth.clearSession();
          notifications.error('انتهت جلستك. سجّل دخولك من جديد.');
          void router.navigate(['/auth/login'], { queryParams: { returnUrl: router.url } });
          return throwError(() => refreshError);
        }),
      );
    }
    if (!(error instanceof HttpErrorResponse && error.status === 401 && isRefresh)) {
      notifications.error(errors.message(error));
    }
    return throwError(() => error);
  }));
};
