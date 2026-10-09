import { NgModule } from '@angular/core';
import { SharedModule } from '../../shared/shared.module';
import { AccountDetailComponent } from './account-detail/account-detail.component';
import { BankingRoutingModule } from './banking-routing.module';
import { HomeComponent } from './home/home.component';
import { OperationComponent } from './operation/operation.component';
import { ReceiptComponent } from './receipt/receipt.component';
import { StatementComponent } from './statement/statement.component';

/** Banca (carga diferida en /banca, requiere sesión): inicio, cuentas, operaciones y extractos. */
@NgModule({
  declarations: [HomeComponent, AccountDetailComponent, OperationComponent, ReceiptComponent, StatementComponent],
  imports: [SharedModule, BankingRoutingModule],
})
export class BankingModule {}
