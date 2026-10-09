// Contratos de /api/auth (backend: Models/DTOs/Auth/AuthDtos.cs).

/** Rol del usuario (backend: UserRole). */
export type UserRole = 'Cliente' | 'Banco';

/** Tipo de cliente (backend: CustomerType). */
export type CustomerType = 'Natural' | 'Juridica';

/** Tipo de documento (backend: DocumentType). */
export type DocumentType = 'CC' | 'CE' | 'PA' | 'NIT';

/** POST /api/auth/login */
export interface LoginRequestDto {
  identifier: string;
  pin: string;
}

export interface AuthUserDto {
  username: string;
  fullName: string;
  role: UserRole;
}

/** Respuesta de login y refresh. El refresh token va en una cookie HttpOnly, no aquí. */
export interface LoginResultDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  user: AuthUserDto;
}

/** POST /api/auth/register */
export interface RegisterRequestDto {
  customerType: CustomerType;
  documentType: DocumentType;
  documentNumber: string;
  fullName: string;
  email: string;
  phone: string;
  cityId: number;
  username: string;
  pin: string;
  pinConfirmation: string;
  acceptTerms: boolean;
}

export interface RegisterResultDto {
  accountNumber: string;
  accountType: 'Ahorros' | 'Corriente';
  originCity: string;
  username: string;
  fullName: string;
}

/** GET /api/auth/username-availability */
export interface UsernameAvailabilityDto {
  username: string;
  available: boolean;
  message: string;
}
