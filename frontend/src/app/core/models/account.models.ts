// Contratos de /api/accounts y /api/cities (backend: Models/DTOs/Accounts y Cities).

export type AccountType = 'Ahorros' | 'Corriente';
export type MovementType = 'Consignacion' | 'Retiro';
export type OperationKind = 'deposit' | 'withdrawal';

export interface CityDto {
  id: number;
  name: string;
}

export interface AccountSummaryDto {
  number: string;
  type: AccountType;
  customerDocument: string;
  customerName: string;
  originCity: string;
  balance: number;
}

export interface MovementDto {
  id: number;
  type: MovementType;
  amount: number;
  city: string;
  balanceAfter: number;
  description: string | null;
  createdAtUtc: string;
}

/** Cuerpo de POST /api/accounts/{number}/deposits y /withdrawals. */
export interface OperationRequestDto {
  amount: number;
  cityId: number;
  description?: string | null;
}

export interface OperationResultDto {
  accountNumber: string;
  balance: number;
  movement: MovementDto;
}

export interface StatementDto {
  accountNumber: string;
  year: number;
  month: number;
  periodStartUtc: string;
  periodEndUtc: string;
  openingBalance: number;
  totalDeposits: number;
  totalWithdrawals: number;
  closingBalance: number;
  movements: MovementDto[];
}
