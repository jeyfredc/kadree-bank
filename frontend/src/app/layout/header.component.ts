import { Component, inject, signal } from '@angular/core';
import { AuthService } from '../core/services/auth.service';

/**
 * Barra superior de la banca (design system: NavHeader). Marino en ambos temas. En pantallas
 * angostas la navegación se pliega en un menú.
 */
@Component({
  selector: 'kb-header',
  standalone: false,
  template: `
    <header class="kb-header" style="flex-wrap: wrap;">
      <button class="kb-icon-btn kb-header__menu" type="button" (click)="menuOpen.update((v) => !v)"
              [attr.aria-expanded]="menuOpen()" aria-controls="kb-nav" aria-label="Abrir menú">
        <span class="kb-icon kb-icon--md" aria-hidden="true">menu</span>
      </button>
      <a class="kb-wordmark" [routerLink]="auth.homeRoute()"><b>Kadree</b> Bank</a>
      <nav class="kb-nav" id="kb-nav" aria-label="Principal" [class.is-open]="menuOpen()" (click)="menuOpen.set(false)">
        <a class="kb-nav__link" routerLink="/banca/inicio" routerLinkActive #inicio="routerLinkActive" [attr.aria-current]="inicio.isActive ? 'page' : null">
          {{ auth.isBanco() ? 'Cuentas' : 'Inicio' }}
        </a>
        <a class="kb-nav__link" routerLink="/banca/extractos" routerLinkActive #ext="routerLinkActive" [attr.aria-current]="ext.isActive ? 'page' : null">Extractos</a>
        @if (auth.isBanco()) {
          <a class="kb-nav__link" routerLink="/admin" routerLinkActive #rep="routerLinkActive" [attr.aria-current]="rep.isActive ? 'page' : null">Administración</a>
        }
      </nav>
      <div class="kb-header__end">
        <span class="kb-user" aria-label="Usuario en sesión">
          <span class="kb-avatar" aria-hidden="true">{{ initials() }}</span>
          <span class="kb-header__name">{{ auth.user()?.fullName }}</span>
        </span>
        <button class="kb-btn kb-btn--secondary kb-btn--md" type="button" (click)="auth.logout()">
          <span class="kb-icon" aria-hidden="true">logout</span>Salir
        </button>
      </div>
    </header>
  `,
})
export class HeaderComponent {
  protected readonly auth = inject(AuthService);
  protected readonly menuOpen = signal(false);

  protected initials(): string {
    const name = this.auth.user()?.fullName ?? '';
    return name.split(' ').filter(Boolean).slice(0, 2).map((p) => p[0]).join('').toUpperCase();
  }
}
