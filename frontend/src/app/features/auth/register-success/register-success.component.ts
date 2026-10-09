import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { FlowStateService } from '../../../core/services/flow-state.service';

/** Cuenta creada: muestra el número de cuenta y lleva al inicio de sesión. */
@Component({
  selector: 'kb-register-success',
  standalone: false,
  template: `
    <div class="kb-auth-page">
      <header class="kb-auth-page__top"><span class="kb-wordmark" style="font-size: 24px;"><b>Kadree</b> Bank</span></header>
      <main class="kb-auth-page__main">
        @if (result(); as r) {
          <section class="kb-auth" aria-labelledby="ok-title" style="max-width: 520px; justify-items: center; text-align: center;">
            <span style="width: 72px; height: 72px; border-radius: 999px; display: inline-flex; align-items: center; justify-content: center; background: var(--success-soft); color: var(--success);">
              <span class="kb-icon kb-icon--fill" aria-hidden="true" style="font-size: 40px; width: 40px; height: 40px;">check_circle</span>
            </span>
            <div class="kb-auth__head" style="justify-items: center;">
              <h1 class="kb-auth__title" id="ok-title">Tu cuenta está lista, {{ r.fullName }}</h1>
              <p class="kb-auth__lead">Abrimos tu cuenta {{ r.accountType === 'Ahorros' ? 'de ahorros' : 'corriente' }}. Guarda estos datos.</p>
            </div>
            <dl class="kb-grid-2" style="margin: 0; width: 100%; padding: 16px 24px; text-align: left; background: var(--bg); border-radius: 12px;">
              <div><dt class="kb-overline">NÚMERO DE CUENTA</dt><dd style="margin: 4px 0 0; font-size: 20px; font-weight: 500;">{{ r.accountNumber }}</dd></div>
              <div><dt class="kb-overline">TIPO</dt><dd style="margin: 4px 0 0; font-size: 20px; font-weight: 500;">{{ r.accountType }}</dd></div>
              <div><dt class="kb-overline">CIUDAD DE ORIGEN</dt><dd style="margin: 4px 0 0;">{{ r.originCity }}</dd></div>
              <div><dt class="kb-overline">USUARIO</dt><dd style="margin: 4px 0 0;">{{ r.username }}</dd></div>
            </dl>
            <a class="kb-btn kb-btn--primary kb-btn--block" routerLink="/acceso/ingresar">Ingresar a mi banca</a>
            <p class="kb-auth__security" style="margin: 0; text-align: left;">
              <span class="kb-icon kb-icon--fill" aria-hidden="true">verified_user</span>
              Tu contraseña de 4 dígitos es solo tuya. Nadie de Kadree Bank te la pedirá.
            </p>
          </section>
        }
      </main>
    </div>
  `,
})
export class RegisterSuccessComponent {
  protected readonly result = inject(FlowStateService).lastRegistration;

  constructor() {
    // Sin un registro reciente (por ejemplo, al recargar), no hay nada que mostrar.
    if (!this.result()) void inject(Router).navigate(['/acceso/registro']);
  }
}
