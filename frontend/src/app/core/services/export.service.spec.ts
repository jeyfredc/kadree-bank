import { ExportColumn, safeFileName, toTextRows } from './export.service';

interface Row {
  city: string;
  count: number;
  total: number;
}

describe('safeFileName', () => {
  it('quita tildes, espacios y símbolos', () => {
    expect(safeFileName('Transacciones por sede · Bogotá 2026-09')).toBe('transacciones-por-sede-bogota-2026-09');
  });
});

describe('toTextRows', () => {
  const columns: ExportColumn<Row>[] = [
    { header: 'Sede', value: (r) => r.city },
    { header: 'Transacciones', value: (r) => r.count, kind: 'number' },
    { header: 'Total', value: (r) => r.total, kind: 'money' },
  ];

  it('formatea números y pesos en formato colombiano', () => {
    const [row] = toTextRows(columns, [{ city: 'Medellín', count: 1500, total: 1250000.5 }]);
    // Intl usa espacios especiales en algunos entornos; se normalizan para comparar.
    expect(row.map((c) => c.replace(/\s/g, ' '))).toEqual(['Medellín', '1.500', '$ 1.250.000,50']);
  });
});
