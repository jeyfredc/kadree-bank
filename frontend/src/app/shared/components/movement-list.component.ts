import { Component, computed, input, signal } from '@angular/core';
import { MovementDto, MovementType } from '../../core/models/account.models';

type Filter = 'all' | MovementType;

interface Row {
  group: string | null;
  movement: MovementDto;
}

const DAY = new Intl.DateTimeFormat('es-CO', { timeZone: 'America/Bogota', day: 'numeric', month: 'long' });
const KEY = new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Bogota' });

/**
 * Lista de movimientos agrupada por día, con filtros por tipo (design system: MovementList).
 * Entradas en verde con «+» e ícono de entrada; salidas en color de texto con «−».
 */
@Component({
  selector: 'kb-movement-list',
  standalone: false,
  template: `
    <section class="kb-movements" [attr.aria-label]="heading()">
      <div class="kb-movements__head">
        <h2 class="kb-movements__title">{{ heading() }}</h2>
        @if (showFilters()) {
          <div class="kb-chips" role="group" aria-label="Filtrar por tipo">
            <button class="kb-chip" type="button" [attr.aria-pressed]="filter() === 'all'" (click)="filter.set('all')">Todos</button>
            <button class="kb-chip" type="button" [attr.aria-pressed]="filter() === 'Consignacion'" (click)="filter.set('Consignacion')">Consignaciones</button>
            <button class="kb-chip" type="button" [attr.aria-pressed]="filter() === 'Retiro'" (click)="filter.set('Retiro')">Retiros</button>
          </div>
        }
      </div>
      @for (row of rows(); track row.movement.id) {
        @if (row.group) {
          <p class="kb-movements__group">{{ row.group }}</p>
        }
        <div class="kb-move" [class.kb-move--in]="row.movement.type === 'Consignacion'" [class.kb-move--out]="row.movement.type === 'Retiro'">
          <span class="kb-move__icon"><span class="kb-icon" aria-hidden="true">{{ row.movement.type === 'Consignacion' ? 'south_west' : 'north_east' }}</span></span>
          <span class="kb-move__text">
            <span class="kb-move__desc">{{ row.movement.description || (row.movement.type === 'Consignacion' ? 'Consignación' : 'Retiro') }}</span>
            <span class="kb-move__meta">{{ row.movement.city }} · {{ row.movement.createdAtUtc | bankDate: 'time' }}</span>
          </span>
          <span class="kb-move__amounts">
            <span class="kb-move__amount">
              <span class="kb-sr">{{ row.movement.type === 'Consignacion' ? 'Consignación de ' : 'Retiro de ' }}</span>
              {{ row.movement.amount | cop: (row.movement.type === 'Consignacion' ? 'signed-in' : 'signed-out') }}
            </span>
            <span class="kb-move__after">Saldo {{ row.movement.balanceAfter | cop }}</span>
          </span>
        </div>
      } @empty {
        <div class="kb-empty"><span class="kb-icon kb-icon--md" aria-hidden="true">receipt_long</span>{{ emptyText() }}</div>
      }
      <ng-content />
    </section>
  `,
})
export class MovementListComponent {
  readonly movements = input.required<MovementDto[]>();
  readonly heading = input('Movimientos recientes');
  readonly showFilters = input(true);
  readonly emptyText = input('No hay movimientos en este período.');

  protected readonly filter = signal<Filter>('all');

  protected readonly rows = computed<Row[]>(() => {
    const f = this.filter();
    const today = KEY.format(new Date());
    let last = '';
    return this.movements()
      .filter((m) => f === 'all' || m.type === f)
      .map((m) => {
        const date = new Date(m.createdAtUtc);
        const key = KEY.format(date);
        const group = key === last ? null : key === today ? 'HOY' : DAY.format(date).toUpperCase();
        last = key;
        return { group, movement: m };
      });
  });
}
