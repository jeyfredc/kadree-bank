import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';
import { errorCode, toErrorMessage } from '../../../core/utils/api-error';

const REMEMBER_KEY = 'kb_usuario_recordado';

/** Inicio de sesión: usuario o número de documento + contraseña de 4 dígitos. */
@Component({
  selector: 'kb-login',
  standalone: false,
  templateUrl: './login.component.html',
})
export class LoginComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  protected readonly form = new FormGroup({
    identifier: new FormControl(readRemembered(), { nonNullable: true, validators: [Validators.required, Validators.maxLength(20)] }),
    pin: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(/^\d{4}$/)] }),
    remember: new FormControl(readRemembered() !== '', { nonNullable: true }),
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly locked = signal<string | null>(null);
  protected readonly sessionEnded = this.route.snapshot.queryParamMap.get('motivo') === 'expirada';

  private readonly identifier = toSignal(this.form.controls.identifier.valueChanges, { initialValue: this.form.controls.identifier.value });

  /** Cómo se interpreta lo escrito: solo dígitos (y puntos) = documento; con letras = usuario. */
  protected readonly identifierKind = computed(() => {
    const value = this.identifier().trim();
    if (!value) return null;
    return /[a-zA-Z]/.test(value) ? 'Usuario' : 'Documento';
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { identifier, pin, remember } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    this.locked.set(null);

    this.auth.login({ identifier: identifier.trim(), pin }).subscribe({
      next: () => {
        rememberIdentifier(remember ? identifier.trim() : '');
        const target = this.route.snapshot.queryParamMap.get('volver') ?? this.auth.homeRoute();
        void this.router.navigateByUrl(target);
      },
      error: (e: unknown) => {
        this.busy.set(false);
        this.form.controls.pin.reset('');
        // Bloqueo (423) se muestra como advertencia; credenciales (401), límite (429) y red, como error.
        if (errorCode(e) === 'acceso_bloqueado') this.locked.set(toErrorMessage(e));
        else this.error.set(toErrorMessage(e));
      },
    });
  }
}

function readRemembered(): string {
  try {
    return localStorage.getItem(REMEMBER_KEY) ?? '';
  } catch {
    return '';
  }
}

function rememberIdentifier(value: string): void {
  try {
    if (value) localStorage.setItem(REMEMBER_KEY, value);
    else localStorage.removeItem(REMEMBER_KEY);
  } catch {
    // Almacenamiento bloqueado: recordar el usuario es solo una comodidad.
  }
}
