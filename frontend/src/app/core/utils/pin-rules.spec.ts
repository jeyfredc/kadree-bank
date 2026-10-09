import { checkPinRules } from './pin-rules';

const okAll = (pin: string, doc = '1012345678') => checkPinRules(pin, doc).every((r) => r.ok);

describe('checkPinRules', () => {
  it('acepta una contraseña que cumple todas las reglas', () => {
    expect(okAll('2580')).toBe(true);
  });

  it('rechaza lo que no son exactamente 4 dígitos', () => {
    expect(okAll('258')).toBe(false);
    expect(okAll('25801')).toBe(false);
    expect(okAll('25a0')).toBe(false);
  });

  it('rechaza el mismo número repetido', () => {
    expect(checkPinRules('1111', '')[1].ok).toBe(false);
  });

  it('rechaza secuencias ascendentes y descendentes', () => {
    expect(checkPinRules('1234', '')[2].ok).toBe(false);
    expect(checkPinRules('4321', '')[2].ok).toBe(false);
    expect(checkPinRules('1235', '')[2].ok).toBe(true);
  });

  it('rechaza los últimos 4 dígitos del documento, aunque el NIT tenga guion', () => {
    expect(checkPinRules('5678', '1012345678')[3].ok).toBe(false);
    expect(checkPinRules('4561', '900123456-1')[3].ok).toBe(false);
  });
});
