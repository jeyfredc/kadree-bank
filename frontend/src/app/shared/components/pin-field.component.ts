import { Component, ElementRef, computed, forwardRef, input, signal, viewChild } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';

/**
 * Contraseña de 4 dígitos (design system: PinField).
 *
 * Es UN solo `<input type="password" inputmode="numeric">` real (accesible, compatible con gestores de
 * contraseñas y con pegar); las cuatro casillas son solo su representación visual. Funciona con
 * formularios reactivos (`formControlName`).
 */
@Component({
  selector: 'kb-pin-field',
  standalone: false,
  providers: [{ provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => PinFieldComponent), multi: true }],
  template: `
    <div class="kb-pin" [class.is-error]="error()" [class.is-disabled]="disabled()" [class.is-focus]="focused()" (click)="focus()">
      <input
        #input
        class="kb-sr"
        type="password"
        inputmode="numeric"
        maxlength="4"
        [id]="inputId()"
        [attr.autocomplete]="autocomplete()"
        [attr.aria-invalid]="error() ? 'true' : null"
        [attr.aria-describedby]="describedBy() || null"
        [value]="value()"
        [disabled]="disabled()"
        (input)="onInput(input.value)"
        (focus)="focused.set(true)"
        (blur)="onBlur()"
      />
      @for (cell of cells(); track $index) {
        <span class="kb-pin__cell" [class.is-filled]="cell.filled" [class.is-current]="cell.current" aria-hidden="true">{{ cell.text }}</span>
      }
      @if (disabled()) {
        <span class="kb-icon" aria-hidden="true" style="color: var(--text-muted)">lock</span>
      } @else {
        <button class="kb-icon-btn kb-pin__toggle" type="button" (click)="toggle($event)" [attr.aria-label]="visible() ? 'Ocultar contraseña' : 'Mostrar contraseña'">
          <span class="kb-icon" aria-hidden="true">{{ visible() ? 'visibility_off' : 'visibility' }}</span>
        </button>
      }
    </div>
  `,
})
export class PinFieldComponent implements ControlValueAccessor {
  /** id del input real, para el `for` de su `<label>`. */
  readonly inputId = input.required<string>();
  /** `current-password` en el ingreso, `new-password` en el registro. */
  readonly autocomplete = input<'current-password' | 'new-password'>('current-password');
  /** Muestra el estado de error (borde rojo en las cuatro casillas). */
  readonly error = input(false);
  /** id del mensaje de ayuda o error asociado. */
  readonly describedBy = input<string | null>(null);

  protected readonly value = signal('');
  protected readonly visible = signal(false);
  protected readonly focused = signal(false);
  protected readonly disabled = signal(false);
  private readonly inputRef = viewChild.required<ElementRef<HTMLInputElement>>('input');

  protected readonly cells = computed(() => {
    const v = this.value();
    return [0, 1, 2, 3].map((i) => ({
      filled: i < v.length,
      current: this.focused() && i === v.length,
      text: i < v.length ? (this.visible() ? v[i] : '•') : '',
    }));
  });

  private onChange: (value: string) => void = () => {};
  private onTouched: () => void = () => {};

  writeValue(value: string | null): void {
    this.value.set((value ?? '').replace(/\D/g, '').slice(0, 4));
  }

  registerOnChange(fn: (value: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  /** Lleva el foco al input real (al hacer clic en cualquier casilla). */
  focus(): void {
    this.inputRef().nativeElement.focus();
  }

  protected onInput(raw: string): void {
    const digits = raw.replace(/\D/g, '').slice(0, 4);
    this.inputRef().nativeElement.value = digits;
    this.value.set(digits);
    this.onChange(digits);
  }

  protected onBlur(): void {
    this.focused.set(false);
    this.onTouched();
  }

  protected toggle(event: Event): void {
    event.stopPropagation();
    this.visible.update((v) => !v);
  }
}
