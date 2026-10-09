import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { forkJoin, switchMap } from 'rxjs';
import { AccountSummaryDto, MovementDto } from '../../../core/models/account.models';
import { AccountsService } from '../../../core/services/accounts.service';
import { AuthService } from '../../../core/services/auth.service';
import { toErrorMessage } from '../../../core/utils/api-error';

/** Detalle de una cuenta: saldo, datos del titular y los últimos 50 movimientos con filtros. */
@Component({
  selector: 'kb-account-detail',
  standalone: false,
  template: `
    <main class="kb-page">
      <nav class="kb-breadcrumb" aria-label="Ruta">
        <a class="kb-link kb-link--muted" routerLink="/banca/inicio">{{ auth.isBanco() ? 'Cuentas' : 'Inicio' }}</a>
        <span aria-hidden="true">›</span>
        <span aria-current="page">{{ account()?.number ?? 'Cuenta' }}</span>
      </nav>

      @if (error()) {
        <kb-alert type="error" heading="No pudimos cargar la cuenta" [message]="error()" />
      }

      @if (account(); as a) {
        <section class="kb-balance" aria-labelledby="acc-title">
          <div class="kb-balance__top">
            <div>
              <h1 class="kb-overline" id="acc-title" style="margin: 0;">
                CUENTA {{ a.type === 'Ahorros' ? 'DE AHORROS' : 'CORRIENTE' }} · {{ a.number }}
              </h1>
              <p class="kb-balance__amount">{{ a.balance | cop }}</p>
              <p class="kb-balance__note">Saldo disponible</p>
            </div>
          </div>
          <div class="kb-balance__split">
            <div class="kb-balance__item"><span class="kb-balance__item-label">Titular</span><span class="kb-balance__item-value">{{ a.customerName }}</span></div>
            <div class="kb-balance__item"><span class="kb-balance__item-label">Documento</span><span class="kb-balance__item-value">{{ a.customerDocument }}</span></div>
            <div class="kb-balance__item"><span class="kb-balance__item-label">Ciudad de origen</span><span class="kb-balance__item-value">{{ a.originCity }}</span></div>
          </div>
        </section>

        <div class="kb-row">
          @if (!auth.isBanco()) {
            <a class="kb-btn kb-btn--primary" routerLink="/banca/consignar" [queryParams]="{ cuenta: a.number }">
              <span class="kb-icon" aria-hidden="true">south_west</span>Consignar
            </a>
            <a class="kb-btn kb-btn--secondary" routerLink="/banca/retirar" [queryParams]="{ cuenta: a.number }">
              <span class="kb-icon" aria-hidden="true">north_east</span>Retirar
            </a>
          }
          <a class="kb-btn kb-btn--tertiary" routerLink="/banca/extractos" [queryParams]="{ cuenta: a.number }">
            <span class="kb-icon" aria-hidden="true">description</span>Ver extracto
          </a>
        </div>

        <kb-movement-list [movements]="movements()" heading="Movimientos" emptyText="Esta cuenta aún no tiene movimientos." />
      } @else if (!error()) {
        <div class="kb-balance" aria-busy="true" aria-label="Cargando">
          <span class="kb-skeleton" style="width: 180px; height: 16px;"></span>
          <span class="kb-skeleton" style="width: 260px; height: 44px;"></span>
        </div>
      }
    </main>
  `,
})
export class AccountDetailComponent {
  private readonly accountsApi = inject(AccountsService);
  protected readonly auth = inject(AuthService);

  protected readonly account = signal<AccountSummaryDto | null>(null);
  protected readonly movements = signal<MovementDto[]>([]);
  protected readonly error = signal<string | null>(null);

  constructor() {
    inject(ActivatedRoute)
      .paramMap.pipe(
        switchMap((params) => {
          const number = params.get('number') ?? '';
          this.account.set(null);
          this.error.set(null);
          return forkJoin([this.accountsApi.account(number), this.accountsApi.movements(number, 50)]);
        }),
        takeUntilDestroyed(),
      )
      .subscribe({
        next: ([account, movements]) => {
          this.account.set(account);
          this.movements.set(movements);
        },
        // Una cuenta ajena responde 404, igual que una inexistente: no se revela que exista.
        error: (e: unknown) => this.error.set(toErrorMessage(e)),
      });
  }
}
