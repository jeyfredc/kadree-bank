import { Pipe, PipeTransform } from '@angular/core';

const formatter = new Intl.NumberFormat('es-CO', { minimumFractionDigits: 0, maximumFractionDigits: 2 });

/**
 * Formatea pesos colombianos: `1234567.5 | cop` → `$ 1.234.567,5`.
 * Con `'signed'` agrega el signo: `+$ 50.000` o `−$ 20.000` (signo menos tipográfico).
 */
@Pipe({ name: 'cop', standalone: false })
export class CopPipe implements PipeTransform {
  transform(value: number | null | undefined, mode: 'plain' | 'signed-in' | 'signed-out' = 'plain'): string {
    if (value === null || value === undefined) return '';
    const text = `$ ${formatter.format(Math.abs(value))}`;
    if (mode === 'signed-in') return `+${text}`;
    if (mode === 'signed-out') return `−${text}`;
    return value < 0 ? `−${text}` : text;
  }
}
