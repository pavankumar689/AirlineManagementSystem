import {
  HttpInterceptorFn,
  HttpErrorResponse,
  HttpRequest,
  HttpHandlerFn
} from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

let isRefreshing = false;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  const isAuthEndpoint = req.url.includes('/api/auth/refresh') ||
                          req.url.includes('/api/auth/login') ||
                          req.url.includes('/api/auth/logout');

  // Prevent sending our internal JWT to external APIs (like Google Gemini)
  const isExternalApi = req.url.includes('generativelanguage.googleapis.com');

  const authorizedReq = (isAuthEndpoint || isExternalApi)
    ? req
    : addToken(req, auth.getToken());

  return next(authorizedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthEndpoint && !isExternalApi && !isRefreshing) {
        return handle401(req, next, auth, router);
      }
      return throwError(() => error);
    })
  );
};

function addToken(req: HttpRequest<any>, token: string | null): HttpRequest<any> {
  if (!token) return req;
  return req.clone({
    headers: req.headers.set('Authorization', `Bearer ${token}`)
  });
}

function handle401(
  originalReq: HttpRequest<any>,
  next: HttpHandlerFn,
  auth: AuthService,
  router: Router
) {
  isRefreshing = true;

  return auth.refreshToken().pipe(
    switchMap(() => {
      isRefreshing = false;
      return next(addToken(originalReq, auth.getToken()));
    }),
    catchError((refreshError) => {
      isRefreshing = false;
      auth.logout();
      router.navigate(['/login']);
      return throwError(() => refreshError);
    })
  );
}