import { Component, inject } from '@angular/core';
import { AuthService } from './core/services/auth.service';

/** Raíz: la barra de navegación aparece solo con sesión; el resto lo pinta el router. */
@Component({
  selector: 'app-root',
  standalone: false,
  template: `
    @if (auth.isLoggedIn()) {
      <kb-header />
    }
    <router-outlet />
  `,
})
export class AppComponent {
  protected readonly auth = inject(AuthService);
}
