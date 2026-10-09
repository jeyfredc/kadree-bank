/** Respuesta estándar de la API (backend: Models/DTOs/Responses/ApiResponse.cs). */
export interface ApiResponse<T = unknown> {
  success: boolean;
  message: string | null;
  /** Código estable del error (p. ej. "saldo_insuficiente"); null cuando success = true. */
  errorCode: string | null;
  errors: string[];
  validationErrors: Record<string, string[]>;
  data?: T;
}

/** Códigos de error que devuelve la API (ApiResponse.errorCode). */
export type ApiErrorCode =
  | 'validacion'
  | 'no_encontrado'
  | 'operacion_concurrente'
  | 'saldo_insuficiente'
  | 'monto_invalido'
  | 'tipo_cuenta_no_permitido'
  | 'credenciales_invalidas'
  | 'acceso_bloqueado'
  | 'sesion_invalida'
  | 'sesion_expirada'
  | 'no_autenticado'
  | 'sin_permiso'
  | 'documento_registrado'
  | 'usuario_no_disponible'
  | 'demasiados_intentos'
  | 'error_interno';
