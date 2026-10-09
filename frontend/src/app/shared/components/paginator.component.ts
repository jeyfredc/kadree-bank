import { Component, computed, input, output } from '@angular/core';

/** Número de página o hueco («…») en la lista de páginas. */
type PageSlot = number | 'gap';

/**
 * Lista compacta de páginas: siempre la primera y la última, y hasta dos vecinas de la actual.
 * Ejemplo con 20 páginas y la 8 actual: 1 … 6 7 8 9 10 … 20.
 * @param current Página actual (desde 1).
 * @param total Total de páginas.
 */
export function pageSlots(current: number, total: number): PageSlot[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const pages = new Set([1, total, current - 2, current - 1, current, current + 1, current + 2]);
  const sorted = [...pages].filter((p) => p >= 1 && p <= total).sort((a, b) => a - b);
  const slots: PageSlot[] = [];
  sorted.forEach((p, i) => {
    if (i > 0 && p - sorted[i - 1] > 1) slots.push('gap');
    slots.push(p);
  });
  return slots;
}

/**
 * Paginador del design system: «Mostrando 21–40 de 135 clientes», anterior/siguiente, números de
 * página y tamaño de página. No guarda estado: el padre decide qué página pedir al servidor.
 */
@Component({
  selector: 'kb-paginator',
  standalone: false,
  template: `
    @if (totalItems() > 0) {
      <nav class="kb-paginator" [attr.aria-label]="'Paginación de ' + itemLabel()">
        <p class="kb-paginator__info" aria-live="polite">
          Mostrando <b>{{ from() }}–{{ to() }}</b> de <b>{{ totalItems() }}</b> {{ itemLabel() }}
        </p>
        <div class="kb-paginator__pages">
          <button class="kb-icon-btn" type="button" (click)="go(page() - 1)" [disabled]="disabled() || page() <= 1" aria-label="Página anterior">
            <span class="kb-icon" aria-hidden="true">chevron_left</span>
          </button>
          @for (slot of slots(); track $index) {
            @if (slot === 'gap') {
              <span class="kb-paginator__gap" aria-hidden="true">…</span>
            } @else {
              <button class="kb-paginator__page" type="button" (click)="go(slot)" [disabled]="disabled()"
                      [class.is-current]="slot === page()" [attr.aria-current]="slot === page() ? 'page' : null"
                      [attr.aria-label]="'Página ' + slot">{{ slot }}</button>
            }
          }
          <button class="kb-icon-btn" type="button" (click)="go(page() + 1)" [disabled]="disabled() || page() >= totalPages()" aria-label="Página siguiente">
            <span class="kb-icon" aria-hidden="true">chevron_right</span>
          </button>
        </div>
        <label class="kb-paginator__size">
          <span>Por página</span>
          <select class="kb-input" (change)="changeSize($event)" [disabled]="disabled()">
            @for (size of sizes(); track size) { <option [value]="size" [selected]="size === pageSize()">{{ size }}</option> }
          </select>
        </label>
      </nav>
    }
  `,
})
export class PaginatorComponent {
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();
  readonly totalItems = input.required<number>();
  readonly totalPages = input.required<number>();
  /** Qué se cuenta, en plural: «clientes», «movimientos». */
  readonly itemLabel = input('resultados');
  readonly sizes = input([10, 20, 50, 100]);
  readonly disabled = input(false);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  protected readonly from = computed(() => (this.page() - 1) * this.pageSize() + 1);
  protected readonly to = computed(() => Math.min(this.page() * this.pageSize(), this.totalItems()));
  protected readonly slots = computed(() => pageSlots(this.page(), this.totalPages()));

  protected go(page: number): void {
    if (page >= 1 && page <= this.totalPages() && page !== this.page()) this.pageChange.emit(page);
  }

  protected changeSize(event: Event): void {
    this.pageSizeChange.emit(Number((event.target as HTMLSelectElement).value));
  }
}
