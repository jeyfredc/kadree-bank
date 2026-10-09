import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { AccountSummaryDto, CityDto, OperationKind } from '../../../core/models/account.models';
import { AccountsService } from '../../../core/services/accounts.service';
import { FlowStateService } from '../../../core/services/flow-state.service';
import { errorCode, toErrorMessage } from '../../../core/utils/api-error';
import { parseCopAmount } from '../../../core/utils/formatting';

const MAX_AMOUNT = 1_000_000_000_000;

/**
 * Consignar o retirar (la ruta indica cuál en `data.kind`). Dos pasos: datos y confirmación.
 * El query param `cuenta` preselecciona la cuenta.
 */
@Component({
  selector: 'kb-operation',
  standalone: false,
  templateUrl: './operation.component.html',
})
export class OperationComponent {
  private readonly accountsApi = inject(AccountsService);
  private readonly flow = inject(FlowStateService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly kind = this.route.snapshot.data['kind'] as OperationKind;
  protected readonly isDeposit = this.kind === 'deposit';
  protected readonly verb = this.isDeposit ? 'Consignar' : 'Retirar';

  protected readonly form = new FormGroup({
    accountNumber: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    amount: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    cityId: new FormControl<number | null>(null, { validators: [Validators.required] }),
    description: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(200)] }),
  });

  protected readonly accounts = signal<AccountSummaryDto[]>([]);
  protected readonly cities = signal<CityDto[]>([]);
  protected readonly loadError = signal<string | null>(null);
  protected readonly error = signal<string | null>(null);
  protected readonly amountError = signal<string | null>(null);
  protected readonly confirming = signal(false);
  protected readonly busy = signal(false);

  private readonly accountNumber = toSignal(this.form.controls.accountNumber.valueChanges, { initialValue: '' });
  private readonly cityId = toSignal(this.form.controls.cityId.valueChanges, { initialValue: null });

  protected readonly selected = computed(() => this.accounts().find((a) => a.number === this.accountNumber()) ?? null);
  protected readonly cityName = computed(() => this.cities().find((c) => c.id === this.cityId())?.name ?? '');
  /** Un retiro fuera de la ciudad de origen es válido, pero entra en el reporte del banco. */
  protected readonly outOfCity = computed(() => !!this.selected() && !!this.cityName() && this.cityName() !== this.selected()!.originCity);
  protected readonly parsedAmount = signal<number | null>(null);

  constructor() {
    forkJoin([this.accountsApi.accounts(), this.accountsApi.cities()])
      .pipe(takeUntilDestroyed())
      .subscribe({
        next: ([accounts, cities]) => {
          this.accounts.set(accounts);
          this.cities.set(cities);
          const wanted = this.route.snapshot.queryParamMap.get('cuenta');
          const account = accounts.find((a) => a.number === wanted) ?? accounts[0];
          if (account) {
            this.form.controls.accountNumber.setValue(account.number);
            // Por defecto, la ciudad de origen de la cuenta.
            this.form.controls.cityId.setValue(cities.find((c) => c.name === account.originCity)?.id ?? null);
          }
        },
        error: (e: unknown) => this.loadError.set(toErrorMessage(e)),
      });
  }

  /** Valida el monto (formato, rango y, para retiros, saldo) y pasa a la confirmación. */
  protected review(): void {
    this.error.set(null);
    this.form.markAllAsTouched();
    const amount = parseCopAmount(this.form.controls.amount.value);
    this.parsedAmount.set(amount);

    if (amount === null) this.amountError.set('Escribe un monto válido, por ejemplo 50.000 o 50.000,50.');
    else if (amount <= 0 || amount > MAX_AMOUNT) this.amountError.set('El monto debe ser mayor que cero y no superar 1.000.000.000.000.');
    else if (!this.isDeposit && this.selected() && amount > this.selected()!.balance)
      this.amountError.set('El monto supera tu saldo disponible.');
    else this.amountError.set(null);

    if (this.amountError() || this.form.invalid) return;
    this.confirming.set(true);
  }

  protected submit(): void {
    const value = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.accountsApi
      .operate(value.accountNumber, this.kind, {
        amount: this.parsedAmount()!,
        cityId: value.cityId ?? 0,
        description: value.description.trim() || null,
      })
      .subscribe({
        next: (result) => {
          this.flow.lastOperation.set({ kind: this.kind, result });
          void this.router.navigate(['/banca/comprobante']);
        },
        error: (e: unknown) => {
          this.busy.set(false);
          this.error.set(toErrorMessage(e));
          // Saldo insuficiente o monto inválido: se corrige en el formulario.
          if (errorCode(e) === 'saldo_insuficiente' || errorCode(e) === 'validacion') this.confirming.set(false);
        },
      });
  }
}
