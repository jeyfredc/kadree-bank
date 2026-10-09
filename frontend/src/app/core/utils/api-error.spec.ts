import { HttpErrorResponse } from '@angular/common/http';
import { errorCode, toErrorMessage } from './api-error';

describe('toErrorMessage', () => {
  it('usa el mensaje del ApiResponse de saldo insuficiente', () => {
    const error = new HttpErrorResponse({
      status: 422,
      error: {
        success: false,
        message: 'La cuenta AH-1 no tiene saldo suficiente.',
        errorCode: 'saldo_insuficiente',
        errors: [],
        validationErrors: {},
      },
    });

    expect(toErrorMessage(error)).toBe('La cuenta AH-1 no tiene saldo suficiente.');
    expect(errorCode(error)).toBe('saldo_insuficiente');
  });

  it('une los mensajes de validación por campo', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        success: false,
        message: 'La solicitud tiene datos inválidos.',
        errorCode: 'validacion',
        errors: [],
        validationErrors: { amount: ['El monto debe ser mayor que cero.'], cityId: ['Debe indicar la ciudad.'] },
      },
    });

    expect(toErrorMessage(error)).toBe('El monto debe ser mayor que cero. Debe indicar la ciudad.');
  });

  it('explica cuando no hay conexión con la API', () => {
    expect(toErrorMessage(new HttpErrorResponse({ status: 0 }))).toContain('No se pudo conectar');
  });
});
