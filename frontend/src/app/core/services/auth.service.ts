import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, tap } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import {
  AuthUserDto,
  LoginRequestDto,
  LoginResultDto,
  RegisterRequestDto,
  RegisterResultDto,
  UsernameAvailabilityDto,
} from '../models/auth.models';

interface Session {
  accessToken: string;
  expiresAt: number;
  user: AuthUserDto;
}

/**
 * Sesión del usuario contra /api/auth.
 *
 * - El access token vive solo en memoria (signal), nunca en localStorage: un script inyectado no lo encuentra.
 * - El refresh token lo maneja el navegador en una cookie HttpOnly limitada a /api/auth; este servicio no lo ve.
 * - Al recargar la página, {@link restoreSession} pide un access token nuevo con esa cookie.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = '/api/auth';
  private readonly session = signal<Session | null>(null);
  private refreshInFlight$: Observable<LoginResultDto> | null = null;

  /** Usuario en sesión, o null. */
  readonly user = computed(() => this.session()?.user ?? null);
  /** true si hay sesión. */
  readonly isLoggedIn = computed(() => this.session() !== null);
  /** true si el usuario es personal del banco. */
  readonly isBanco = computed(() => this.session()?.user.role === 'Banco');

  /** Access token actual para el encabezado Authorization, o null. */
  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  /** Ruta inicial según el rol: administración para el administrador, inicio para los clientes. */
  homeRoute(): string {
    return this.isBanco() ? '/admin' : '/banca/inicio';
  }

  /**
   * Inicia sesión con usuario o documento y la contraseña de 4 dígitos.
   * @param request Identificación y contraseña.
   * @returns El resultado; los errores llegan como HttpErrorResponse (401, 423, 429).
   */
  login(request: LoginRequestDto): Observable<LoginResultDto> {
    return this.http
      .post<ApiResponse<LoginResultDto>>(`${this.baseUrl}/login`, request)
      .pipe(map(unwrap), tap((result) => this.setSession(result)));
  }

  /**
   * Renueva el access token con la cookie del refresh token. Si varias peticiones lo piden a la vez,
   * se hace una sola llamada y todas esperan su resultado.
   */
  refresh(): Observable<LoginResultDto> {
    if (!this.refreshInFlight$) {
      this.refreshInFlight$ = this.http.post<ApiResponse<LoginResultDto>>(`${this.baseUrl}/refresh`, null).pipe(
        map(unwrap),
        tap((result) => this.setSession(result)),
        finalize(() => (this.refreshInFlight$ = null)),
        shareReplay(1),
      );
    }
    return this.refreshInFlight$;
  }

  /** Intenta recuperar la sesión al abrir la aplicación. Nunca falla: sin cookie válida, queda sin sesión. */
  restoreSession(): Promise<void> {
    return firstValueFrom(this.refresh().pipe(map(() => undefined), catchError(() => of(undefined))));
  }

  /**
   * Cierra la sesión en el servidor (revoca el refresh token), limpia la memoria y lleva al inicio de sesión.
   * @param reason Motivo para mostrar en la pantalla de ingreso (por ejemplo, sesión expirada).
   */
  logout(reason?: 'expirada'): void {
    this.http.post(`${this.baseUrl}/logout`, null).pipe(catchError(() => of(null))).subscribe();
    this.endSession(reason);
  }

  /** Limpia la sesión local sin llamar al servidor (por ejemplo, cuando el refresh falló). */
  endSession(reason?: 'expirada'): void {
    this.session.set(null);
    void this.router.navigate(['/acceso/ingresar'], { queryParams: reason ? { motivo: reason } : {} });
  }

  /**
   * Crea cliente, cuenta y usuario.
   * @param request Datos del formulario de registro.
   */
  register(request: RegisterRequestDto): Observable<RegisterResultDto> {
    return this.http.post<ApiResponse<RegisterResultDto>>(`${this.baseUrl}/register`, request).pipe(map(unwrap));
  }

  /**
   * Consulta si un nombre de usuario cumple las reglas y está libre.
   * @param username Usuario a consultar.
   */
  checkUsername(username: string): Observable<UsernameAvailabilityDto> {
    const params = new HttpParams().set('username', username);
    return this.http
      .get<ApiResponse<UsernameAvailabilityDto>>(`${this.baseUrl}/username-availability`, { params })
      .pipe(map(unwrap));
  }

  private setSession(result: LoginResultDto): void {
    this.session.set({
      accessToken: result.accessToken,
      expiresAt: Date.parse(result.accessTokenExpiresAtUtc),
      user: result.user,
    });
  }
}

/** Extrae `data` de un ApiResponse exitoso. */
export function unwrap<T>(response: ApiResponse<T>): T {
  return response.data as T;
}
