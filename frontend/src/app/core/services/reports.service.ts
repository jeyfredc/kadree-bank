import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { EMPTY, Observable, expand, map, reduce } from 'rxjs';
import { ApiResponse } from '../models/api-response.model';
import { MAX_PAGE_SIZE, PageRequest, PagedResult } from '../models/paged-result.model';
import {
  BranchReportQuery,
  BranchTransactionsPageDto,
  CustomerTransactionCountDto,
  OutOfCityWithdrawalsDto,
} from '../models/report.models';
import { unwrap } from './auth.service';

/**
 * Reportes (/api/reports). Solo rol Banco; un cliente recibe 403. Todos llegan paginados desde el
 * servidor; {@link fetchAllPages} junta todas las páginas cuando se necesita el resultado completo
 * (por ejemplo, para exportar).
 */
@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);

  /**
   * Reporte por sede: clientes con su número de transacciones en el mes por sede (ciudad donde se
   * hicieron), de mayor a menor, con el detalle de cada transacción. Paginado por cliente.
   * @param query Año, mes y filtros opcionales de sede (`cityId`) y tipo (`Consignacion` o `Retiro`).
   * @param paging Página y clientes por página.
   */
  transactionsByBranch(query: BranchReportQuery, paging: PageRequest): Observable<BranchTransactionsPageDto> {
    let params = pageParams(paging).set('year', query.year).set('month', query.month);
    if (query.cityId) params = params.set('cityId', query.cityId);
    if (query.type) params = params.set('type', query.type);
    return this.http
      .get<ApiResponse<BranchTransactionsPageDto>>('/api/reports/transactions-by-branch', { params })
      .pipe(map(unwrap));
  }

  /**
   * Reporte 1: transacciones por cliente en un mes, de mayor a menor. Paginado por cliente.
   * @param year Año.
   * @param month Mes 1-12.
   * @param paging Página y clientes por página.
   */
  transactionsByCustomer(year: number, month: number, paging: PageRequest): Observable<PagedResult<CustomerTransactionCountDto>> {
    const params = pageParams(paging).set('year', year).set('month', month);
    return this.http
      .get<ApiResponse<PagedResult<CustomerTransactionCountDto>>>('/api/reports/transactions-by-customer', { params })
      .pipe(map(unwrap));
  }

  /**
   * Reporte 2: retiros fuera de la ciudad de origen que superan el umbral. Paginado por cliente.
   * @param minTotal Umbral en pesos.
   * @param from Fecha inicial opcional yyyy-MM-dd.
   * @param to Fecha final opcional yyyy-MM-dd.
   * @param paging Página y clientes por página.
   */
  outOfCityWithdrawals(minTotal: number, from: string | undefined, to: string | undefined, paging: PageRequest): Observable<PagedResult<OutOfCityWithdrawalsDto>> {
    let params = pageParams(paging).set('minTotal', minTotal);
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return this.http
      .get<ApiResponse<PagedResult<OutOfCityWithdrawalsDto>>>('/api/reports/out-of-city-withdrawals', { params })
      .pipe(map(unwrap));
  }
}

/**
 * Pide todas las páginas de un reporte, una tras otra, con el tamaño máximo, y junta sus elementos.
 * @param fetchPage Función que pide una página.
 * @returns Todos los elementos, en el orden del servidor.
 */
export function fetchAllPages<T>(fetchPage: (paging: PageRequest) => Observable<PagedResult<T>>): Observable<T[]> {
  return fetchPage({ page: 1, pageSize: MAX_PAGE_SIZE }).pipe(
    expand((result) =>
      result.page < result.totalPages ? fetchPage({ page: result.page + 1, pageSize: MAX_PAGE_SIZE }) : EMPTY,
    ),
    reduce((all, result) => all.concat(result.items), [] as T[]),
  );
}

function pageParams(paging: PageRequest): HttpParams {
  return new HttpParams().set('page', paging.page).set('pageSize', paging.pageSize);
}
