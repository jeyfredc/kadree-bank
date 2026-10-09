import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { FlowStateService } from '../../../core/services/flow-state.service';

/** Comprobante de la última consignación o retiro. Al recargar no hay operación y vuelve al inicio. */
@Component({
  selector: 'kb-receipt',
  standalone: false,
  template: `
    @if (operation(); as op) {
      <main class="kb-page" style="max-width: 560px;">
        <section class="kb-card" style="display: grid; gap: var(--space-6); justify-items: center; text-align: center;" aria-labelledby="rc-title">
          <span style="width: 64px; height: 64px; border-radius: 999px; display: inline-flex; align-items: center; justify-content: center; background: var(--success-soft); color: var(--success);">
            <span class="kb-icon kb-icon--fill" aria-hidden="true" style="font-size: 36px; width: 36px; height: 36px;">check_circle</span>
          </span>
          <div>
            <h1 class="kb-card__title" id="rc-title" role="status">{{ op.kind === 'deposit' ? 'Consignación exitosa' : 'Retiro exitoso' }}</h1>
            <p class="kb-balance__amount" style="margin: var(--space-2) 0 0;">
              {{ op.result.movement.amount | cop: (op.kind === 'deposit' ? 'signed-in' : 'signed-out') }}
            </p>
          </div>
          <dl class="kb-summary" style="width: 100%; text-align: left;">
            <div><dt>Cuenta</dt><dd>{{ op.result.accountNumber }}</dd></div>
            <div><dt>Ciudad</dt><dd>{{ op.result.movement.city }}</dd></div>
            @if (op.result.movement.description) {
              <div><dt>Descripción</dt><dd>{{ op.result.movement.description }}</dd></div>
            }
            <div><dt>Fecha y hora</dt><dd>{{ op.result.movement.createdAtUtc | bankDate: 'datetime' }}</dd></div>
            <div><dt>Comprobante</dt><dd>N.º {{ op.result.movement.id }}</dd></div>
            <div class="is-total"><dt>Nuevo saldo</dt><dd>{{ op.result.balance | cop }}</dd></div>
          </dl>
          <div class="kb-auth__actions" style="width: 100%;">
            <a class="kb-btn kb-btn--tertiary" [routerLink]="['/banca/cuentas', op.result.accountNumber]">Ver la cuenta</a>
            <a class="kb-btn kb-btn--primary" routerLink="/banca/inicio">Volver al inicio</a>
          </div>
        </section>
      </main>
    }
  `,
})
export class ReceiptComponent {
  protected readonly operation = inject(FlowStateService).lastOperation;

  constructor() {
    if (!this.operation()) void inject(Router).navigate(['/banca/inicio']);
  }
}
