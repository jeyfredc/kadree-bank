import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import {
  AccountSummaryDto,
  CityDto,
  MovementDto,
  OperationKind,
  OperationRequestDto,
  OperationResultDto,
  StatementDto,
} from '../models/account.models';
import { ApiResponse } from '../models/api-response.model';
import { unwrap } from './auth.service';

/**
 * Cuentas y ciudades (/api/accounts, /api/cities). El token lo agrega AuthInterceptor.
 * Un cliente solo recibe sus propias cuentas; una cuenta ajena responde 404.
 */
@Injectable({ providedIn: 'root' })
export class AccountsService {
  private readonly http = inject(HttpClient);

  /** Ciudades del catálogo (no requiere sesión; se usa también en el registro). */
  cities(): Observable<CityDto[]> {
    return this.http.get<ApiResponse<CityDto[]>>('/api/cities').pipe(map(unwrap));
  }

  /** Cuentas visibles para el usuario: las suyas (cliente) o todas (banco). */
  accounts(): Observable<AccountSummaryDto[]> {
    return this.http.get<ApiResponse<AccountSummaryDto[]>>('/api/accounts').pipe(map(unwrap));
  }

  /**
   * Saldo y datos de una cuenta.
   * @param number Número de cuenta, por ejemplo AH-1001.
   */
  account(number: string): Observable<AccountSummaryDto> {
    return this.http.get<ApiResponse<AccountSummaryDto>>(`/api/accounts/${encodeURIComponent(number)}`).pipe(map(unwrap));
  }

  /**
   * Movimientos más recientes, del más nuevo al más antiguo.
   * @param number Número de cuenta.
   * @param limit Cantidad (1-100).
   */
  movements(number: string, limit = 20): Observable<MovementDto[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http
      .get<ApiResponse<MovementDto[]>>(`/api/accounts/${encodeURIComponent(number)}/movements`, { params })
      .pipe(map(unwrap));
  }

  /**
   * Extracto de un mes (en hora de Bogotá).
   * @param number Número de cuenta.
   * @param year Año.
   * @param month Mes 1-12.
   */
  statement(number: string, year: number, month: number): Observable<StatementDto> {
    return this.http
      .get<ApiResponse<StatementDto>>(`/api/accounts/${encodeURIComponent(number)}/statements/${year}/${month}`)
      .pipe(map(unwrap));
  }

  /**
   * Consigna o retira. Un retiro sin fondos falla con 422 (errorCode 'saldo_insuficiente').
   * @param number Número de cuenta.
   * @param kind 'deposit' o 'withdrawal'.
   * @param request Monto, ciudad y descripción.
   */
  operate(number: string, kind: OperationKind, request: OperationRequestDto): Observable<OperationResultDto> {
    const path = kind === 'deposit' ? 'deposits' : 'withdrawals';
    return this.http
      .post<ApiResponse<OperationResultDto>>(`/api/accounts/${encodeURIComponent(number)}/${path}`, request)
      .pipe(map(unwrap));
  }
}
