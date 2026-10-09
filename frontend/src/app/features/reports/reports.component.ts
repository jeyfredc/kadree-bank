import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Observable, firstValueFrom } from 'rxjs';
import { CityDto, MovementType } from '../../core/models/account.models';
import { PagedResult } from '../../core/models/paged-result.model';
import {
  BranchReportQuery,
  BranchTransactionDto,
  BranchTransactionsPageDto,
  CustomerTransactionCountDto,
  OutOfCityWithdrawalsDto,
} from '../../core/models/report.models';
import { AccountsService } from '../../core/services/accounts.service';
import { ExportColumn, ExportService } from '../../core/services/export.service';
import { ReportsService, fetchAllPages } from '../../core/services/reports.service';
import { toErrorMessage } from '../../core/utils/api-error';
import { currentMonthValue, formatBogotaDate, formatBogotaDateTime, parseCopAmount, parseMonthValue } from '../../core/utils/formatting';

type ExportFormat = 'excel' | 'pdf';

const MONTH_NAME = new Intl.DateTimeFormat('es-CO', { month: 'long', year: 'numeric', timeZone: 'UTC' });

const MOTIVE_LABEL: Record<MovementType, string> = { Consignacion: 'Consignación', Retiro: 'Retiro' };

const BRANCH_COLUMNS: ExportColumn<BranchTransactionDto>[] = [
  { header: 'Sede', value: (r) => r.city },
  { header: 'Cliente', value: (r) => r.fullName },
  { header: 'Documento', value: (r) => r.documentNumber },
  { header: 'Total del cliente', value: (r) => r.customerTransactionCount, kind: 'number' },
  { header: 'Fecha y hora', value: (r) => formatBogotaDateTime(r.createdAtUtc) },
  { header: 'Motivo', value: (r) => MOTIVE_LABEL[r.type] },
  { header: 'Monto', value: (r) => r.amount, kind: 'money' },
  { header: 'Cuenta', value: (r) => r.accountNumber },
  { header: 'Descripción', value: (r) => r.description ?? '' },
  { header: 'Comprobante', value: (r) => r.movementId, kind: 'number' },
];

const CUSTOMER_COLUMNS: ExportColumn<CustomerTransactionCountDto>[] = [
  { header: 'Cliente', value: (r) => r.fullName },
  { header: 'Documento', value: (r) => r.documentNumber },
  { header: 'Transacciones', value: (r) => r.transactionCount, kind: 'number' },
  { header: 'Primera transacción', value: (r) => formatBogotaDate(r.firstTransactionAtUtc) },
  { header: 'Última transacción', value: (r) => formatBogotaDate(r.lastTransactionAtUtc) },
];

const OUT_OF_CITY_COLUMNS: ExportColumn<OutOfCityWithdrawalsDto>[] = [
  { header: 'Cliente', value: (r) => r.fullName },
  { header: 'Documento', value: (r) => r.documentNumber },
  { header: 'Retiros', value: (r) => r.withdrawalCount, kind: 'number' },
  { header: 'Total retirado', value: (r) => r.totalWithdrawn, kind: 'money' },
  { header: 'Primer retiro', value: (r) => formatBogotaDate(r.firstTransactionAtUtc) },
  { header: 'Último retiro', value: (r) => formatBogotaDate(r.lastTransactionAtUtc) },
];

/** Filas de un mismo cliente en una sede, para combinar sus celdas comunes en la tabla. */
interface BranchGroup {
  key: string;
  rows: BranchTransactionDto[];
}

/** Filtros con que se generó la tabla visible: la paginación y la exportación usan estos, no el formulario. */
interface CustomerQuery {
  year: number;
  month: number;
}
interface WithdrawalsQuery {
  threshold: number;
  from?: string;
  to?: string;
}

/**
 * Administración (solo el usuario administrador, rol Banco). Las tres consultas se paginan en el
 * servidor; cambiar de página repite la consulta con los mismos filtros y otra página. Las descargas
 * en Excel o PDF piden TODAS las páginas del filtro.
 * - Transacciones por sede: filtros de mes, sede y tipo; paginada por cliente.
 * - Reporte 1: transacciones por cliente en un mes.
 * - Reporte 2: clientes con retiros fuera de su ciudad de origen que superan un umbral.
 */
@Component({
  selector: 'kb-reports',
  standalone: false,
  templateUrl: './reports.component.html',
})
export class ReportsComponent {
  private readonly reports = inject(ReportsService);
  private readonly exporter = inject(ExportService);

  protected readonly maxMonth = currentMonthValue();
  /** Fecha de una fila en hora de Bogotá (dd/MM/yyyy, o «—» si no hay). */
  protected readonly bogotaDate = formatBogotaDate;
  protected readonly motiveLabel = MOTIVE_LABEL;
  protected readonly cities = signal<CityDto[]>([]);

  protected readonly branchForm = new FormGroup({
    month: new FormControl(this.maxMonth, { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{4}-\d{2}$/)] }),
    cityId: new FormControl<number | null>(null),
    type: new FormControl<MovementType | null>(null),
  });
  protected readonly monthForm = new FormGroup({
    month: new FormControl(this.maxMonth, { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{4}-\d{2}$/)] }),
  });
  protected readonly withdrawalsForm = new FormGroup({
    minTotal: new FormControl('1.000.000', { nonNullable: true, validators: [Validators.required] }),
    from: new FormControl('', { nonNullable: true }),
    to: new FormControl('', { nonNullable: true }),
  });

  // --- Transacciones por sede (paginada por cliente) ---
  protected readonly branch = signal<BranchTransactionsPageDto | null>(null);
  protected readonly branchPageSize = signal(10);
  protected readonly branchError = signal<string | null>(null);
  protected readonly branchBusy = signal(false);
  /** Descripción de los filtros con que se generó la tabla visible (se usa también al exportar). */
  protected readonly branchFilters = signal('');
  private branchQuery: BranchReportQuery | null = null;
  /** Filas de la página agrupadas por sede y cliente, en el orden del servidor (de mayor a menor total). */
  protected readonly branchGroups = computed<BranchGroup[]>(() => {
    const groups: BranchGroup[] = [];
    for (const row of this.branch()?.items ?? []) {
      const key = `${row.cityId}-${row.customerId}`;
      const last = groups.at(-1);
      if (last?.key === key) last.rows.push(row);
      else groups.push({ key, rows: [row] });
    }
    return groups;
  });

  // --- Reporte 1 ---
  protected readonly report1 = signal<PagedResult<CustomerTransactionCountDto> | null>(null);
  protected readonly report1PageSize = signal(20);
  protected readonly report1Error = signal<string | null>(null);
  protected readonly report1Busy = signal(false);
  protected readonly report1Filters = signal('');
  private report1Query: CustomerQuery | null = null;

  // --- Reporte 2 ---
  protected readonly report2 = signal<PagedResult<OutOfCityWithdrawalsDto> | null>(null);
  protected readonly report2PageSize = signal(20);
  protected readonly report2Error = signal<string | null>(null);
  protected readonly report2Busy = signal(false);
  protected readonly report2Threshold = signal(0);
  protected readonly report2Filters = signal('');
  private report2Query: WithdrawalsQuery | null = null;

  /** Exportación en curso, para deshabilitar el botón y mostrar el estado de carga. */
  protected readonly exporting = signal<string | null>(null);
  protected readonly exportError = signal<string | null>(null);

  constructor() {
    inject(AccountsService).cities().pipe(takeUntilDestroyed()).subscribe({
      next: (cities) => this.cities.set(cities),
      error: () => this.cities.set([]),
    });
    this.loadBranch();
    this.loadReport1();
    this.loadReport2();
  }

  // ---------------------------------------------------------------- Transacciones por sede

  /** Toma los filtros del formulario y consulta la página 1. */
  protected loadBranch(): void {
    const { month, cityId, type } = this.branchForm.getRawValue();
    const period = parseMonthValue(month);
    if (!period) {
      this.branchError.set('Elige un mes válido.');
      return;
    }
    const city = this.cities().find((c) => c.id === cityId)?.name ?? 'Todas';
    const typeLabel = type === 'Consignacion' ? 'Solo consignaciones' : type === 'Retiro' ? 'Solo retiros' : 'Consignaciones y retiros';
    this.branchQuery = { year: period.year, month: period.month, cityId, type };
    this.branchFilters.set(`Mes: ${monthLabel(period.year, period.month)} · Sede: ${city} · Tipo: ${typeLabel}`);
    this.fetchBranch(1);
  }

  protected goBranchPage(page: number): void {
    this.fetchBranch(page);
  }

  protected changeBranchPageSize(size: number): void {
    this.branchPageSize.set(size);
    this.fetchBranch(1);
  }

  private fetchBranch(page: number): void {
    const query = this.branchQuery;
    if (!query) return;
    this.branchBusy.set(true);
    this.branchError.set(null);
    this.reports.transactionsByBranch(query, { page, pageSize: this.branchPageSize() }).subscribe({
      next: (result) => {
        this.branch.set(result);
        this.branchBusy.set(false);
      },
      error: (e: unknown) => {
        this.branchError.set(toErrorMessage(e));
        this.branchBusy.set(false);
      },
    });
  }

  // ---------------------------------------------------------------- Reporte 1

  protected loadReport1(): void {
    const period = parseMonthValue(this.monthForm.controls.month.value);
    if (!period) {
      this.report1Error.set('Elige un mes válido.');
      return;
    }
    this.report1Query = { year: period.year, month: period.month };
    this.report1Filters.set(`Mes: ${monthLabel(period.year, period.month)}`);
    this.fetchReport1(1);
  }

  protected goReport1Page(page: number): void {
    this.fetchReport1(page);
  }

  protected changeReport1PageSize(size: number): void {
    this.report1PageSize.set(size);
    this.fetchReport1(1);
  }

  private fetchReport1(page: number): void {
    const query = this.report1Query;
    if (!query) return;
    this.report1Busy.set(true);
    this.report1Error.set(null);
    this.reports.transactionsByCustomer(query.year, query.month, { page, pageSize: this.report1PageSize() }).subscribe({
      next: (result) => {
        this.report1.set(result);
        this.report1Busy.set(false);
      },
      error: (e: unknown) => {
        this.report1Error.set(toErrorMessage(e));
        this.report1Busy.set(false);
      },
    });
  }

  // ---------------------------------------------------------------- Reporte 2

  protected loadReport2(): void {
    const { minTotal, from, to } = this.withdrawalsForm.getRawValue();
    const threshold = parseCopAmount(minTotal);
    if (threshold === null || threshold < 0) {
      this.report2Error.set('Escribe un umbral válido en pesos, por ejemplo 1.000.000.');
      return;
    }
    if (from && to && from > to) {
      this.report2Error.set('La fecha inicial no puede ser posterior a la final.');
      return;
    }
    this.report2Query = { threshold, from: from || undefined, to: to || undefined };
    this.report2Filters.set(`Umbral: más de $ ${threshold.toLocaleString('es-CO')} · Desde: ${from || 'el inicio'} · Hasta: ${to || 'hoy'}`);
    this.fetchReport2(1);
  }

  protected goReport2Page(page: number): void {
    this.fetchReport2(page);
  }

  protected changeReport2PageSize(size: number): void {
    this.report2PageSize.set(size);
    this.fetchReport2(1);
  }

  private fetchReport2(page: number): void {
    const query = this.report2Query;
    if (!query) return;
    this.report2Busy.set(true);
    this.report2Error.set(null);
    this.reports.outOfCityWithdrawals(query.threshold, query.from, query.to, { page, pageSize: this.report2PageSize() }).subscribe({
      next: (result) => {
        this.report2.set(result);
        this.report2Threshold.set(query.threshold);
        this.report2Busy.set(false);
      },
      error: (e: unknown) => {
        this.report2Error.set(toErrorMessage(e));
        this.report2Busy.set(false);
      },
    });
  }

  // ---------------------------------------------------------------- Limpiar

  /** Vuelve a los filtros iniciales (mes actual, todas las sedes, ambos tipos) y consulta. */
  protected clearBranch(): void {
    this.branchForm.reset({ month: this.maxMonth, cityId: null, type: null });
    this.loadBranch();
  }

  /** Vuelve al mes actual y consulta. */
  protected clearReport1(): void {
    this.monthForm.reset({ month: this.maxMonth });
    this.loadReport1();
  }

  /** Quita las fechas, vuelve al umbral de $1.000.000 y consulta todo el histórico. */
  protected clearReport2(): void {
    this.withdrawalsForm.reset({ minTotal: '1.000.000', from: '', to: '' });
    this.loadReport2();
  }

  // ---------------------------------------------------------------- Exportar (todas las páginas)

  protected exportBranch(format: ExportFormat): void {
    const query = this.branchQuery;
    if (!query) return;
    this.export(format, 'branch', fetchAllPages((paging) => this.reports.transactionsByBranch(query, paging)), {
      fileName: `transacciones-por-sede ${this.branchFilters()}`,
      title: 'Kadree Bank · Transacciones por sede',
      subtitle: this.branchFilters(),
      columns: BRANCH_COLUMNS,
    });
  }

  protected exportReport1(format: ExportFormat): void {
    const query = this.report1Query;
    if (!query) return;
    this.export(format, 'report1', fetchAllPages((paging) => this.reports.transactionsByCustomer(query.year, query.month, paging)), {
      fileName: `transacciones-por-cliente ${this.report1Filters()}`,
      title: 'Kadree Bank · Transacciones por cliente',
      subtitle: this.report1Filters(),
      columns: CUSTOMER_COLUMNS,
    });
  }

  protected exportReport2(format: ExportFormat): void {
    const query = this.report2Query;
    if (!query) return;
    this.export(format, 'report2', fetchAllPages((paging) => this.reports.outOfCityWithdrawals(query.threshold, query.from, query.to, paging)), {
      fileName: 'retiros-fuera-de-la-ciudad-de-origen',
      title: 'Kadree Bank · Retiros fuera de la ciudad de origen',
      subtitle: this.report2Filters(),
      columns: OUT_OF_CITY_COLUMNS,
    });
  }

  /** Pide todas las páginas del filtro y genera el archivo con ellas. */
  private export<T>(
    format: ExportFormat,
    key: string,
    allRows$: Observable<T[]>,
    table: { fileName: string; title: string; subtitle: string; columns: ExportColumn<T>[] },
  ): void {
    this.exporting.set(`${key}-${format}`);
    this.exportError.set(null);
    firstValueFrom(allRows$)
      .then((rows) => (format === 'excel' ? this.exporter.toExcel({ ...table, rows }) : this.exporter.toPdf({ ...table, rows })))
      .catch(() => this.exportError.set('No pudimos generar el archivo. Intenta de nuevo.'))
      .finally(() => this.exporting.set(null));
  }
}

function monthLabel(year: number, month: number): string {
  return MONTH_NAME.format(new Date(Date.UTC(year, month - 1, 1)));
}
