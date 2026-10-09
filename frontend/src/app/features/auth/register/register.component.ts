import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { CityDto } from '../../../core/models/account.models';
import { ApiResponse } from '../../../core/models/api-response.model';
import { CustomerType, DocumentType, RegisterRequestDto, UsernameAvailabilityDto } from '../../../core/models/auth.models';
import { AccountsService } from '../../../core/services/accounts.service';
import { AuthService } from '../../../core/services/auth.service';
import { FlowStateService } from '../../../core/services/flow-state.service';
import { toErrorMessage } from '../../../core/utils/api-error';
import { checkPinRules } from '../../../core/utils/pin-rules';

type Step = 1 | 2 | 3;

const STEP1_FIELDS = ['customerType', 'documentType', 'documentNumber', 'fullName', 'email', 'phone', 'cityId'];

/** Creación de cuenta en tres pasos: tus datos, tu acceso y confirmación. */
@Component({
  selector: 'kb-register',
  standalone: false,
  templateUrl: './register.component.html',
})
export class RegisterComponent {
  private readonly auth = inject(AuthService);
  private readonly accounts = inject(AccountsService);
  private readonly flow = inject(FlowStateService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly datos = new FormGroup({
    customerType: new FormControl<CustomerType>('Natural', { nonNullable: true }),
    documentType: new FormControl<DocumentType>('CC', { nonNullable: true }),
    documentNumber: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(20)] }),
    fullName: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(150)] }),
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email, Validators.maxLength(150)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^3\d{9}$/)] }),
    cityId: new FormControl<number | null>(null, { validators: [Validators.required] }),
  });

  protected readonly acceso = new FormGroup({
    username: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^(?=.*[a-z])[a-z0-9.]{6,20}$/)] }),
    pin: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{4}$/)] }),
    pinConfirmation: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    acceptTerms: new FormControl(false, { nonNullable: true }),
  });

  protected readonly step = signal<Step>(1);
  protected readonly cities = signal<CityDto[]>([]);
  protected readonly busy = signal(false);
  protected readonly generalError = signal<string | null>(null);
  protected readonly serverErrors = signal<Record<string, string[]>>({});
  protected readonly usernameStatus = signal<UsernameAvailabilityDto | null>(null);
  protected readonly triedStep2 = signal(false);

  private readonly customerType = toSignal(this.datos.controls.customerType.valueChanges, { initialValue: 'Natural' as CustomerType });
  private readonly pin = toSignal(this.acceso.controls.pin.valueChanges, { initialValue: '' });
  private readonly pinConfirmation = toSignal(this.acceso.controls.pinConfirmation.valueChanges, { initialValue: '' });
  private readonly documentNumber = toSignal(this.datos.controls.documentNumber.valueChanges, { initialValue: '' });
  private readonly acceptTerms = toSignal(this.acceso.controls.acceptTerms.valueChanges, { initialValue: false });

  protected readonly isEmpresa = computed(() => this.customerType() === 'Juridica');
  protected readonly documentTypes = computed(() =>
    this.isEmpresa()
      ? [{ value: 'NIT' as DocumentType, label: 'NIT' }]
      : [
          { value: 'CC' as DocumentType, label: 'Cédula de ciudadanía' },
          { value: 'CE' as DocumentType, label: 'Cédula de extranjería' },
          { value: 'PA' as DocumentType, label: 'Pasaporte' },
        ],
  );
  protected readonly pinRules = computed(() => checkPinRules(this.pin(), this.documentNumber()));
  protected readonly pinsMatch = computed(() => this.pin().length === 4 && this.pin() === this.pinConfirmation());
  protected readonly step2Ready = computed(() => this.pinRules().every((r) => r.ok) && this.pinsMatch() && this.acceptTerms());
  protected readonly cityName = computed(() => this.cities().find((c) => c.id === this.datos.controls.cityId.value)?.name ?? '');

  constructor() {
    this.accounts.cities().pipe(takeUntilDestroyed()).subscribe({
      next: (cities) => this.cities.set(cities),
      error: (e: unknown) => this.generalError.set(toErrorMessage(e)),
    });
    // Persona natural → cédula por defecto; empresa → NIT.
    this.datos.controls.customerType.valueChanges.pipe(takeUntilDestroyed()).subscribe((type) =>
      this.datos.controls.documentType.setValue(type === 'Juridica' ? 'NIT' : 'CC'),
    );
  }

  /** Mensaje de error de un campo: primero el del servidor, luego el de la validación local. */
  protected fieldError(name: string): string | null {
    const server = this.serverErrors()[name];
    if (server?.length) return server[0];
    const control = (this.datos.get(name) ?? this.acceso.get(name)) as FormControl | null;
    if (!control || !control.touched || control.valid) return null;
    return LOCAL_MESSAGES[name] ?? 'Revisa este campo.';
  }

  protected selectType(type: CustomerType): void {
    this.datos.controls.customerType.setValue(type);
  }

  protected next(): void {
    if (this.step() === 1) {
      if (this.datos.invalid) {
        this.datos.markAllAsTouched();
        return;
      }
      this.step.set(2);
    } else if (this.step() === 2) {
      this.triedStep2.set(true);
      if (this.acceso.invalid || !this.step2Ready() || this.usernameStatus()?.available === false) {
        this.acceso.markAllAsTouched();
        return;
      }
      this.step.set(3);
    }
  }

  protected back(): void {
    if (this.step() === 1) void this.router.navigate(['/acceso/ingresar']);
    else this.step.update((s) => (s - 1) as Step);
  }

  protected goTo(step: Step): void {
    this.step.set(step);
  }

  /** Consulta la disponibilidad del usuario al salir del campo. */
  protected checkUsername(): void {
    const control = this.acceso.controls.username;
    control.setValue(control.value.trim().toLowerCase(), { emitEvent: false });
    this.clearServerError('username');
    if (control.invalid) {
      this.usernameStatus.set(null);
      return;
    }
    this.auth.checkUsername(control.value).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (status) => this.usernameStatus.set(status),
      error: () => this.usernameStatus.set(null),
    });
  }

  protected clearServerError(name: string): void {
    if (this.serverErrors()[name]) {
      const { [name]: _, ...rest } = this.serverErrors();
      this.serverErrors.set(rest);
    }
  }

  protected submit(): void {
    const d = this.datos.getRawValue();
    const a = this.acceso.getRawValue();
    const request: RegisterRequestDto = {
      customerType: d.customerType,
      documentType: d.documentType,
      documentNumber: d.documentNumber.trim(),
      fullName: d.fullName.trim(),
      email: d.email.trim(),
      phone: d.phone.trim(),
      cityId: d.cityId ?? 0,
      username: a.username.trim().toLowerCase(),
      pin: a.pin,
      pinConfirmation: a.pinConfirmation,
      acceptTerms: a.acceptTerms,
    };
    this.busy.set(true);
    this.generalError.set(null);

    this.auth.register(request).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.flow.lastRegistration.set(result);
        void this.router.navigate(['/acceso/registro/listo']);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        const body = e instanceof HttpErrorResponse ? (e.error as Partial<ApiResponse> | null) : null;
        const fields = body?.validationErrors ?? {};
        this.serverErrors.set(fields);
        this.generalError.set(toErrorMessage(e));
        const names = Object.keys(fields);
        if (names.length) this.step.set(names.some((n) => STEP1_FIELDS.includes(n)) ? 1 : 2);
      },
    });
  }
}

const LOCAL_MESSAGES: Record<string, string> = {
  documentNumber: 'Escribe tu número de documento.',
  fullName: 'Escribe tu nombre completo (mínimo 3 caracteres).',
  email: 'Escribe un correo válido, por ejemplo nombre@correo.com.',
  phone: 'El celular tiene 10 dígitos y empieza por 3, sin espacios.',
  cityId: 'Elige la ciudad.',
  username: 'De 6 a 20 caracteres: letras minúsculas, números y punto, con al menos una letra.',
  pin: 'La contraseña tiene exactamente 4 dígitos.',
  pinConfirmation: 'Confirma tu contraseña.',
};
