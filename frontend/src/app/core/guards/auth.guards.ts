import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/** Exige sesión; sin ella, lleva al inicio de sesión y recuerda adónde quería ir. */
export const authGuard: CanMatchFn = (_route, segments) => {
  const auth = inject(AuthService);
  if (auth.isLoggedIn()) return true;
  const returnUrl = '/' + segments.map((s) => s.path).join('/');
  return inject(Router).createUrlTree(['/acceso/ingresar'], { queryParams: { volver: returnUrl } });
};

/** Exige rol Banco (reportes); un cliente vuelve a su inicio. La API igual responde 403. */
export const bancoGuard: CanMatchFn = () => {
  const auth = inject(AuthService);
  return auth.isBanco() ? true : inject(Router).createUrlTree(['/banca/inicio']);
};

/** Exige rol Cliente (consignar y retirar); el rol Banco consulta pero no opera. La API igual responde 403. */
export const clienteGuard: CanMatchFn = () => {
  const auth = inject(AuthService);
  return auth.isBanco() ? inject(Router).createUrlTree(['/banca/inicio']) : true;
};

/** Solo para quien no tiene sesión (ingreso y registro); con sesión, lleva a su inicio. */
export const guestGuard: CanMatchFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() ? inject(Router).createUrlTree([auth.homeRoute()]) : true;
};
