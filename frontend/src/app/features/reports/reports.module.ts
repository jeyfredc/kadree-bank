import { NgModule } from '@angular/core';
import { SharedModule } from '../../shared/shared.module';
import { ReportsComponent } from './reports.component';
import { ReportsRoutingModule } from './reports-routing.module';

/** Administración (carga diferida en /admin, solo el administrador): reportes y descargas. */
@NgModule({
  declarations: [ReportsComponent],
  imports: [SharedModule, ReportsRoutingModule],
})
export class ReportsModule {}
