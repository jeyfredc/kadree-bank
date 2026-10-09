import { CopPipe } from './cop.pipe';

describe('CopPipe', () => {
  const pipe = new CopPipe();
  // Intl usa espacios especiales en algunos entornos; se normalizan para comparar.
  const fmt = (...args: Parameters<CopPipe['transform']>) => pipe.transform(...args).replace(/\s/g, ' ');

  it('formatea con punto de miles y coma decimal', () => {
    expect(fmt(1234567.5)).toBe('$ 1.234.567,5');
    expect(fmt(50000)).toBe('$ 50.000');
  });

  it('agrega el signo de entrada o salida', () => {
    expect(fmt(50000, 'signed-in')).toBe('+$ 50.000');
    expect(fmt(20000, 'signed-out')).toBe('−$ 20.000');
  });

  it('devuelve vacío para null', () => {
    expect(pipe.transform(null)).toBe('');
  });
});
