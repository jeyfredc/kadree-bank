import { NgModule } from '@angular/core';
import { SharedModule } from '../../shared/shared.module';
import { AuthRoutingModule } from './auth-routing.module';
import { LoginComponent } from './login/login.component';
import { RegisterSuccessComponent } from './register-success/register-success.component';
import { RegisterComponent } from './register/register.component';

/** Acceso (carga diferida en /acceso): inicio de sesión, registro y cuenta creada. */
@NgModule({
  declarations: [LoginComponent, RegisterComponent, RegisterSuccessComponent],
  imports: [SharedModule, AuthRoutingModule],
})
export class AuthModule {}
