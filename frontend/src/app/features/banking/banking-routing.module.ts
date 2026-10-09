import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { clienteGuard } from '../../core/guards/auth.guards';
import { AccountDetailComponent } from './account-detail/account-detail.component';
import { HomeComponent } from './home/home.component';
import { OperationComponent } from './operation/operation.component';
import { ReceiptComponent } from './receipt/receipt.component';
import { StatementComponent } from './statement/statement.component';

const routes: Routes = [
  { path: 'inicio', component: HomeComponent, title: 'Inicio · Kadree Bank' },
  { path: 'cuentas/:number', component: AccountDetailComponent, title: 'Cuenta · Kadree Bank' },
  { path: 'consignar', component: OperationComponent, canMatch: [clienteGuard], data: { kind: 'deposit' }, title: 'Consignar · Kadree Bank' },
  { path: 'retirar', component: OperationComponent, canMatch: [clienteGuard], data: { kind: 'withdrawal' }, title: 'Retirar · Kadree Bank' },
  { path: 'comprobante', component: ReceiptComponent, canMatch: [clienteGuard], title: 'Comprobante · Kadree Bank' },
  { path: 'extractos', component: StatementComponent, title: 'Extractos · Kadree Bank' },
  { path: '', pathMatch: 'full', redirectTo: 'inicio' },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class BankingRoutingModule {}
