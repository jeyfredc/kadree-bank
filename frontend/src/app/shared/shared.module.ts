import { CommonModule } from '@angular/common';
import { NgModule } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { AlertComponent } from './components/alert.component';
import { MovementListComponent } from './components/movement-list.component';
import { PaginatorComponent } from './components/paginator.component';
import { PinFieldComponent } from './components/pin-field.component';
import { BankDatePipe } from './pipes/bank-date.pipe';
import { CopPipe } from './pipes/cop.pipe';

/**
 * Piezas del design system reutilizadas por los módulos de funcionalidad, más los módulos de Angular
 * que todos necesitan (formularios, router, pipes comunes).
 */
@NgModule({
  declarations: [PinFieldComponent, AlertComponent, MovementListComponent, PaginatorComponent, CopPipe, BankDatePipe],
  imports: [CommonModule],
  exports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    RouterModule,
    PinFieldComponent,
    AlertComponent,
    MovementListComponent,
    PaginatorComponent,
    CopPipe,
    BankDatePipe,
  ],
})
export class SharedModule {}
