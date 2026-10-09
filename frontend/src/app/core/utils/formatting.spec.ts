import { currentMonthValue, formatBogotaDate, formatBogotaDateTime, parseCopAmount, parseMonthValue, todayValue } from './formatting';

describe('formatBogotaDateTime', () => {
  it('muestra fecha y hora de Bogotá (03:30 UTC del 1-oct es 30-sep a las 22:30)', () => {
    expect(formatBogotaDateTime('2026-10-01T03:30:00Z').replace(/\s/g, ' ')).toBe('30/09/2026 · 22:30');
  });
});

describe('formatBogotaDate', () => {
  it('muestra la fecha en hora de Bogotá (03:30 UTC del 1-oct es 30-sep)', () => {
    expect(formatBogotaDate('2026-10-01T03:30:00Z')).toBe('30/09/2026');
    expect(formatBogotaDate('2026-09-02T12:30:00Z')).toBe('02/09/2026');
  });

  it('muestra un guion si no hay fecha', () => {
    expect(formatBogotaDate(null)).toBe('—');
  });
});

describe('parseCopAmount', () => {
  it('interpreta punto de miles y coma decimal', () => {
    expect(parseCopAmount('50000')).toBe(50000);
    expect(parseCopAmount('50.000')).toBe(50000);
    expect(parseCopAmount('$ 1.250.000,75')).toBe(1250000.75);
    expect(parseCopAmount('0,5')).toBe(0.5);
  });

  it('rechaza textos que no son montos o tienen más de dos decimales', () => {
    expect(parseCopAmount('')).toBeNull();
    expect(parseCopAmount('abc')).toBeNull();
    expect(parseCopAmount('10,001')).toBeNull();
    expect(parseCopAmount('5.00')).toBeNull();
    expect(parseCopAmount('-100')).toBeNull();
  });
});

describe('fechas en hora de Bogotá', () => {
  it('a las 2 a. m. UTC del 1 de noviembre todavía es octubre en Bogotá', () => {
    const now = new Date('2026-11-01T02:00:00Z');
    expect(currentMonthValue(now)).toBe('2026-10');
    expect(todayValue(now)).toBe('2026-10-31');
  });

  it('separa el valor de un input month', () => {
    expect(parseMonthValue('2026-03')).toEqual({ year: 2026, month: 3 });
    expect(parseMonthValue('03-2026')).toBeNull();
  });
});
