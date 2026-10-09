import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { authGuard, bancoGuard } from './core/guards/auth.guards';

/**
 * Rutas raíz. Cada área es un módulo con carga diferida:
 * /acceso (ingreso y registro), /banca (cliente) y /admin (administración, solo el administrador).
 */
const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'banca/inicio' },
  { path: 'acceso', loadChildren: () => import('./features/auth/auth.module').then((m) => m.AuthModule) },
  { path: 'banca', canMatch: [authGuard], loadChildren: () => import('./features/banking/banking.module').then((m) => m.BankingModule) },
  { path: 'admin', canMatch: [authGuard, bancoGuard], loadChildren: () => import('./features/reports/reports.module').then((m) => m.ReportsModule) },
  // Ruta anterior de los reportes; se conserva para enlaces guardados.
  { path: 'reportes', pathMatch: 'full', redirectTo: 'admin' },
  { path: '**', redirectTo: 'banca/inicio' },
];

@NgModule({
  imports: [RouterModule.forRoot(routes, { scrollPositionRestoration: 'top' })],
  exports: [RouterModule],
})
export class AppRoutingModule {}
