import { HttpErrorResponse } from '@angular/common/http';
import { ApiResponse } from '../models/api-response.model';

/**
 * Convierte cualquier error de la API en un mensaje legible para el usuario.
 *
 * Prioridad: errores por campo (`validationErrors`) → `message` del `ApiResponse` → código HTTP.
 * Si no hubo respuesta (status 0), explica que no hay conexión con la API.
 * @param error Error recibido en el `subscribe({ error })`.
 * @returns Mensaje en español listo para mostrar.
 */
export function toErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Ocurrió un error inesperado.';
  }
  if (error.status === 0) {
    return 'No se pudo conectar con el servidor. Verifique que la API esté en ejecución.';
  }

  const body = error.error as Partial<ApiResponse> | null;
  const fieldMessages = Object.values(body?.validationErrors ?? {}).flat();
  if (fieldMessages.length > 0) {
    return fieldMessages.join(' ');
  }
  return body?.message ?? `Error ${error.status}.`;
}

/**
 * Obtiene el código estable del error para reaccionar sin interpretar el mensaje.
 * @param error Error recibido en el `subscribe({ error })`.
 * @returns El `errorCode` del `ApiResponse` (p. ej. `'saldo_insuficiente'`), o `undefined` si no hay.
 */
export function errorCode(error: unknown): string | undefined {
  return error instanceof HttpErrorResponse
    ? ((error.error as Partial<ApiResponse> | null)?.errorCode ?? undefined)
    : undefined;
}
