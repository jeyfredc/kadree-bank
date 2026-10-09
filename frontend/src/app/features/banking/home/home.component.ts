import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AccountSummaryDto, MovementDto } from '../../../core/models/account.models';
import { AccountsService } from '../../../core/services/accounts.service';
import { AuthService } from '../../../core/services/auth.service';
import { toErrorMessage } from '../../../core/utils/api-error';

const HIDE_KEY = 'kb_ocultar_saldos';

/**
 * Inicio de la banca. Cliente: saldo total, accesos rápidos, sus productos y los movimientos
 * recientes de su primera cuenta. Banco: todas las cuentas (solo consulta).
 */
@Component({
  selector: 'kb-home',
  standalone: false,
  templateUrl: './home.component.html',
})
export class HomeComponent {
  private readonly accountsApi = inject(AccountsService);
  protected readonly auth = inject(AuthService);

  protected readonly accounts = signal<AccountSummaryDto[] | null>(null);
  protected readonly recent = signal<MovementDto[] | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly hidden = signal(readHidden());

  protected readonly total = computed(() => (this.accounts() ?? []).reduce((sum, a) => sum + a.balance, 0));
  protected readonly firstName = computed(() => this.auth.user()?.fullName.split(' ')[0] ?? '');

  constructor() {
    this.accountsApi.accounts().pipe(takeUntilDestroyed()).subscribe({
      next: (accounts) => {
        this.accounts.set(accounts);
        if (!this.auth.isBanco() && accounts.length) this.loadRecent(accounts[0].number);
        else this.recent.set([]);
      },
      error: (e: unknown) => this.error.set(toErrorMessage(e)),
    });
  }

  protected toggleHidden(): void {
    this.hidden.update((v) => !v);
    try {
      localStorage.setItem(HIDE_KEY, this.hidden() ? '1' : '0');
    } catch {
      // Sin almacenamiento local la preferencia dura solo esta visita.
    }
  }

  private loadRecent(number: string): void {
    this.accountsApi.movements(number, 5).subscribe({
      next: (movements) => this.recent.set(movements),
      error: () => this.recent.set([]),
    });
  }
}

function readHidden(): boolean {
  try {
    return localStorage.getItem(HIDE_KEY) === '1';
  } catch {
    return false;
  }
}
