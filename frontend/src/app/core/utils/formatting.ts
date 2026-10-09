/** Colombia no tiene horario de verano: el desfase fijo basta para el DatePipe. */
export const BANK_UTC_OFFSET = '-0500';

/**
 * Mes actual en hora de Bogotá, en el formato de un `<input type="month">`.
 * @param now Fecha de referencia (por defecto, ahora). Útil en pruebas.
 * @returns Texto `yyyy-MM`, por ejemplo `'2026-10'`.
 */
export function currentMonthValue(now = new Date()): string {
  const bogota = new Date(now.getTime() - 5 * 60 * 60 * 1000);
  return `${bogota.getUTCFullYear()}-${String(bogota.getUTCMonth() + 1).padStart(2, '0')}`;
}

/**
 * Separa el valor de un `<input type="month">` en año y mes.
 * @param value Texto `yyyy-MM`.
 * @returns Año y mes numéricos, o `null` si el formato no es válido.
 */
export function parseMonthValue(value: string): { year: number; month: number } | null {
  const match = /^(\d{4})-(\d{2})$/.exec(value);
  return match ? { year: Number(match[1]), month: Number(match[2]) } : null;
}

/**
 * Interpreta un monto escrito en formato colombiano: punto de miles y coma decimal.
 * Acepta `50000`, `50.000`, `$ 50.000,5` y `1.250.000,75`.
 * @param text Texto escrito por el usuario.
 * @returns El monto, o `null` si el texto no es un número con máximo dos decimales.
 */
export function parseCopAmount(text: string): number | null {
  const clean = text.replace(/[$\s]/g, '');
  if (!/^(\d{1,3}(\.\d{3})+|\d+)(,\d{1,2})?$/.test(clean)) return null;
  return Number(clean.replace(/\./g, '').replace(',', '.'));
}

const BOGOTA_DATE = new Intl.DateTimeFormat('es-CO', {
  timeZone: 'America/Bogota',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

const BOGOTA_DATE_TIME = new Intl.DateTimeFormat('es-CO', {
  timeZone: 'America/Bogota',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: false,
});

/**
 * Fecha y hora en hora de Bogotá, con el mismo formato que el pipe `bankDate`.
 * @param utc Fecha ISO en UTC.
 * @returns `'dd/MM/yyyy · HH:mm'`, por ejemplo `'30/09/2026 · 22:30'`, o `'—'` si no hay fecha.
 */
export function formatBogotaDateTime(utc: string | null | undefined): string {
  return utc ? BOGOTA_DATE_TIME.format(new Date(utc)).replace(',', ' ·') : '—';
}

/**
 * Fecha de una fila de reporte en hora de Bogotá.
 * @param utc Fecha ISO en UTC, o null si no hubo transacciones.
 * @returns `'dd/MM/yyyy'`, por ejemplo `'30/09/2026'`, o `'—'` si no hay fecha.
 */
export function formatBogotaDate(utc: string | null | undefined): string {
  return utc ? BOGOTA_DATE.format(new Date(utc)) : '—';
}

/**
 * Hoy en hora de Bogotá, en el formato de un `<input type="date">`.
 * @param now Fecha de referencia (por defecto, ahora).
 * @returns Texto `yyyy-MM-dd`.
 */
export function todayValue(now = new Date()): string {
  return new Date(now.getTime() - 5 * 60 * 60 * 1000).toISOString().slice(0, 10);
}
