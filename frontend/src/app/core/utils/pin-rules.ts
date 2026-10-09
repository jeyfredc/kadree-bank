/** Resultado de una regla de la contraseña, para la lista que se marca en vivo en el registro. */
export interface PinRuleCheck {
  label: string;
  ok: boolean;
}

/**
 * Reglas de una contraseña nueva de 4 dígitos. Es el espejo de backend/Helpers/PinRules.cs:
 * aquí solo sirve para guiar al usuario; la validación que cuenta es la del servidor.
 * @param pin Contraseña propuesta.
 * @param documentNumber Documento del cliente (no puede terminar igual que la contraseña).
 * @returns Cada regla con su estado.
 */
export function checkPinRules(pin: string, documentNumber: string): PinRuleCheck[] {
  const fourDigits = /^\d{4}$/.test(pin);
  const docDigits = documentNumber.replace(/\D/g, '');
  const isSequence = (step: number) => [...pin].every((d, i) => i === 0 || d.charCodeAt(0) - pin.charCodeAt(i - 1) === step);
  return [
    { label: 'Tiene 4 dígitos', ok: fourDigits },
    { label: 'No repite el mismo número (1111)', ok: fourDigits && new Set(pin).size > 1 },
    { label: 'No es una secuencia (1234 o 4321)', ok: fourDigits && !isSequence(1) && !isSequence(-1) },
    { label: 'Es distinta de los últimos 4 dígitos de tu documento', ok: fourDigits && (docDigits.length < 4 || docDigits.slice(-4) !== pin) },
  ];
}
