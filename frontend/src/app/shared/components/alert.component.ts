import { Component, computed, input } from '@angular/core';

export type AlertType = 'success' | 'error' | 'warning' | 'info';

const ICONS: Record<AlertType, string> = { success: 'check_circle', error: 'error', warning: 'warning', info: 'info' };

/**
 * Mensaje del banco (design system: Alert). El tipo se reconoce por ícono y título, no solo por color.
 * Los errores usan role="alert" (se anuncian de inmediato); el resto, role="status".
 * El contenido proyectado se muestra debajo del texto (por ejemplo, un botón de acción).
 */
@Component({
  selector: 'kb-alert',
  standalone: false,
  template: `
    <div class="kb-alert kb-alert--{{ type() }}" [attr.role]="type() === 'error' ? 'alert' : 'status'">
      <span class="kb-icon kb-icon--fill" aria-hidden="true">{{ icon() }}</span>
      <div class="kb-alert__body">
        @if (heading()) {
          <p class="kb-alert__title">{{ heading() }}</p>
        }
        @if (message()) {
          <p class="kb-alert__text">{{ message() }}</p>
        }
        <ng-content />
      </div>
    </div>
  `,
})
export class AlertComponent {
  readonly type = input<AlertType>('info');
  readonly heading = input<string | null>(null);
  readonly message = input<string | null>(null);
  protected readonly icon = computed(() => ICONS[this.type()]);
}
