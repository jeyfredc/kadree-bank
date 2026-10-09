import { Pipe, PipeTransform } from '@angular/core';

const TIME_ZONE = 'America/Bogota';
const formats = {
  datetime: new Intl.DateTimeFormat('es-CO', { timeZone: TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false }),
  date: new Intl.DateTimeFormat('es-CO', { timeZone: TIME_ZONE, day: '2-digit', month: '2-digit', year: 'numeric' }),
  time: new Intl.DateTimeFormat('es-CO', { timeZone: TIME_ZONE, hour: '2-digit', minute: '2-digit', hour12: false }),
  day: new Intl.DateTimeFormat('es-CO', { timeZone: TIME_ZONE, day: 'numeric', month: 'long' }),
};

/** Fecha UTC de la API mostrada en hora de Bogotá: `datetime` 08/10/2026 20:07, `date`, `time` o `day` (8 de octubre). */
@Pipe({ name: 'bankDate', standalone: false })
export class BankDatePipe implements PipeTransform {
  transform(value: string | null | undefined, format: keyof typeof formats = 'datetime'): string {
    if (!value) return '';
    return formats[format].format(new Date(value)).replace(',', ' ·');
  }
}
