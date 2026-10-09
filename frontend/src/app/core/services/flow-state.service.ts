import { Injectable, signal } from '@angular/core';
import { OperationKind, OperationResultDto } from '../models/account.models';
import { RegisterResultDto } from '../models/auth.models';

/** Resultado de la última operación, para la pantalla de comprobante. */
export interface LastOperation {
  kind: OperationKind;
  result: OperationResultDto;
}

/**
 * Estado que pasa de una pantalla a la siguiente dentro de un flujo (operación → comprobante,
 * registro → cuenta creada). Vive solo en memoria: al recargar, esas pantallas vuelven al inicio.
 */
@Injectable({ providedIn: 'root' })
export class FlowStateService {
  /** Última consignación o retiro confirmado. */
  readonly lastOperation = signal<LastOperation | null>(null);

  /** Último registro exitoso. */
  readonly lastRegistration = signal<RegisterResultDto | null>(null);
}
