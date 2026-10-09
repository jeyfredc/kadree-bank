// Contratos de /api/reports (backend: Models/DTOs/Reports/ReportDtos.cs). Solo rol Banco.

import { MovementType } from './account.models';
import { PagedResult } from './paged-result.model';

/**
 * Página del reporte por sede. Se pagina por CLIENTE: `totalItems` y `pageSize` cuentan clientes
 * (bloques cliente-sede) y `items` trae todas las transacciones de los clientes de la página.
 */
export interface BranchTransactionsPageDto extends PagedResult<BranchTransactionDto> {
  /** Total de transacciones de todas las páginas con los filtros. */
  totalTransactions: number;
}

/**
 * Fila del reporte por sede (GET /api/reports/transactions-by-branch): UNA transacción de un cliente
 * en una sede. Las filas de un cliente llegan seguidas y en orden cronológico.
 */
export interface BranchTransactionDto {
  cityId: number;
  city: string;
  customerId: number;
  documentNumber: string;
  fullName: string;
  /** Total del cliente en la sede; se repite en cada fila y define el orden de mayor a menor. */
  customerTransactionCount: number;
  /** Número de comprobante. */
  movementId: number;
  accountNumber: string;
  /** Motivo: consignación o retiro. */
  type: MovementType;
  amount: number;
  description: string | null;
  createdAtUtc: string;
}

/** Filtros del reporte por sede. Sin `cityId` = todas las sedes; sin `type` = ambos tipos. */
export interface BranchReportQuery {
  year: number;
  month: number;
  cityId?: number | null;
  type?: MovementType | null;
}

export interface CustomerTransactionCountDto {
  customerId: number;
  documentNumber: string;
  fullName: string;
  transactionCount: number;
  /** Null si el cliente no tuvo transacciones en el mes. */
  firstTransactionAtUtc: string | null;
  lastTransactionAtUtc: string | null;
}

export interface OutOfCityWithdrawalsDto {
  customerId: number;
  documentNumber: string;
  fullName: string;
  totalWithdrawn: number;
  withdrawalCount: number;
  firstTransactionAtUtc: string;
  lastTransactionAtUtc: string;
}
