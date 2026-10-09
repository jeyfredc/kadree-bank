import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Endpoints de sesión: no llevan Bearer y un 401 aquí no dispara una renovación. */
const SESSION_ENDPOINTS = ['/api/auth/login', '/api/auth/refresh', '/api/auth/logout', '/api/auth/register'];

/**
 * Agrega `Authorization: Bearer <token>` a las llamadas a /api. Si la API responde 401 (access token
 * vencido), renueva la sesión con la cookie del refresh token UNA vez y reintenta la petición. Si la
 * renovación falla, cierra la sesión y lleva al inicio de sesión.
 */
@Injectable()
export class AuthInterceptor implements HttpInterceptor {
  private readonly auth = inject(AuthService);

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (!request.url.startsWith('/api/')) {
      return next.handle(request);
    }
    const isSessionCall = SESSION_ENDPOINTS.some((url) => request.url.startsWith(url));

    return next.handle(this.withToken(request, isSessionCall)).pipe(
      catchError((error: unknown) => {
        if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isSessionCall || !this.auth.isLoggedIn()) {
          return throwError(() => error);
        }
        return this.auth.refresh().pipe(
          switchMap(() => next.handle(this.withToken(request, false))),
          catchError((refreshError: unknown) => {
            this.auth.endSession('expirada');
            return throwError(() => refreshError);
          }),
        );
      }),
    );
  }

  private withToken(request: HttpRequest<unknown>, skip: boolean): HttpRequest<unknown> {
    const token = this.auth.accessToken;
    return skip || !token ? request : request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
  }
}
