import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { AccountSummaryDto, StatementDto } from '../../../core/models/account.models';
import { AccountsService } from '../../../core/services/accounts.service';
import { toErrorMessage } from '../../../core/utils/api-error';
import { currentMonthValue, parseMonthValue } from '../../../core/utils/formatting';

/**
 * Extracto mensual: saldo inicial, total consignado, total retirado, saldo final y movimientos.
 * Usa el tema claro del design system (documento imprimible).
 */
@Component({
  selector: 'kb-statement',
  standalone: false,
  template: `
    <div data-theme="claro" style="background: var(--bg); color: var(--text); min-height: calc(100vh - 64px);">
      <main class="kb-page">
        <div>
          <h1 class="kb-page__title">Extractos</h1>
          <p class="kb-page__lead">Elige la cuenta y el mes. Las fechas están en hora de Colombia.</p>
        </div>

        @if (loadError()) {
          <kb-alert type="error" heading="No pudimos cargar tus cuentas" [message]="loadError()" />
        }

        <form class="kb-card kb-row" style="align-items: flex-end;" [formGroup]="form" (ngSubmit)="load()" novalidate>
          <div class="kb-field" style="flex: 1 1 240px;">
            <label class="kb-field__label" for="st-account">Cuenta</label>
            <div class="kb-input-box">
              <select class="kb-input" id="st-account" formControlName="accountNumber">
                @for (a of accounts(); track a.number) { <option [value]="a.number">{{ a.type }} {{ a.number }}</option> }
              </select>
              <span class="kb-icon" aria-hidden="true">expand_more</span>
            </div>
          </div>
          <div class="kb-field" style="flex: 1 1 180px;">
            <label class="kb-field__label" for="st-month">Mes</label>
            <div class="kb-input-box">
              <input class="kb-input" id="st-month" type="month" formControlName="month" [max]="maxMonth" />
            </div>
          </div>
          <button class="kb-btn kb-btn--primary" type="submit" [disabled]="busy() || form.invalid" [class.is-loading]="busy()">Consultar</button>
        </form>

        @if (error()) {
          <kb-alert type="error" heading="No pudimos generar el extracto" [message]="error()" />
        }

        @if (statement(); as s) {
          <section class="kb-card" style="display: grid; gap: var(--space-6);" aria-labelledby="st-title">
            <div class="kb-row" style="justify-content: space-between; align-items: center;">
              <div>
                <h2 class="kb-card__title" id="st-title">Extracto {{ s.accountNumber }}</h2>
                <p class="kb-card__meta">{{ s.periodStartUtc | bankDate: 'date' }} – {{ lastDay(s) | bankDate: 'date' }}</p>
              </div>
              <button class="kb-btn kb-btn--secondary kb-btn--md" type="button" (click)="print()">
                <span class="kb-icon" aria-hidden="true">print</span>Imprimir
              </button>
            </div>
            <dl class="kb-summary">
              <div><dt>Saldo inicial</dt><dd>{{ s.openingBalance | cop }}</dd></div>
              <div><dt>Total consignado</dt><dd style="color: var(--success);">{{ s.totalDeposits | cop: 'signed-in' }}</dd></div>
              <div><dt>Total retirado</dt><dd>{{ s.totalWithdrawals | cop: 'signed-out' }}</dd></div>
              <div class="is-total"><dt>Saldo final</dt><dd>{{ s.closingBalance | cop }}</dd></div>
            </dl>
          </section>
          <kb-movement-list [movements]="s.movements" heading="Movimientos del mes" />
        }
      </main>
    </div>
  `,
})
export class StatementComponent {
  private readonly accountsApi = inject(AccountsService);

  protected readonly maxMonth = currentMonthValue();
  protected readonly form = new FormGroup({
    accountNumber: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    month: new FormControl(this.maxMonth, { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{4}-\d{2}$/)] }),
  });

  protected readonly accounts = signal<AccountSummaryDto[]>([]);
  protected readonly statement = signal<StatementDto | null>(null);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    const wanted = inject(ActivatedRoute).snapshot.queryParamMap.get('cuenta');
    this.accountsApi.accounts().pipe(takeUntilDestroyed()).subscribe({
      next: (accounts) => {
        this.accounts.set(accounts);
        const account = accounts.find((a) => a.number === wanted) ?? accounts[0];
        if (account) {
          this.form.controls.accountNumber.setValue(account.number);
          this.load();
        }
      },
      error: (e: unknown) => this.loadError.set(toErrorMessage(e)),
    });
  }

  protected load(): void {
    const { accountNumber, month } = this.form.getRawValue();
    const period = parseMonthValue(month);
    if (!accountNumber || !period) return;
    this.busy.set(true);
    this.error.set(null);
    this.accountsApi.statement(accountNumber, period.year, period.month).subscribe({
      next: (s) => {
        this.statement.set(s);
        this.busy.set(false);
      },
      error: (e: unknown) => {
        this.statement.set(null);
        this.error.set(toErrorMessage(e));
        this.busy.set(false);
      },
    });
  }

  protected print(): void {
    window.print();
  }

  /** El período termina en el primer instante del mes siguiente; se muestra el día anterior. */
  protected lastDay(s: StatementDto): string {
    return new Date(new Date(s.periodEndUtc).getTime() - 1).toISOString();
  }
}
