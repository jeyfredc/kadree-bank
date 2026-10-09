import { registerLocaleData } from '@angular/common';
import { HTTP_INTERCEPTORS, provideHttpClient, withInterceptorsFromDi } from '@angular/common/http';
import localeEsCo from '@angular/common/locales/es-CO';
import {
  LOCALE_ID,
  NgModule,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection,
} from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { AuthInterceptor } from './core/interceptors/auth.interceptor';
import { AuthService } from './core/services/auth.service';
import { HeaderComponent } from './layout/header.component';
import { SharedModule } from './shared/shared.module';

registerLocaleData(localeEsCo);

/**
 * Módulo raíz. La aplicación es "zoneless": el estado de las pantallas vive en signals.
 * Antes de mostrar la primera pantalla intenta recuperar la sesión con la cookie del refresh token.
 */
@NgModule({
  declarations: [AppComponent, HeaderComponent],
  imports: [BrowserModule, AppRoutingModule, SharedModule],
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideHttpClient(withInterceptorsFromDi()),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true },
    { provide: LOCALE_ID, useValue: 'es-CO' },
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}
