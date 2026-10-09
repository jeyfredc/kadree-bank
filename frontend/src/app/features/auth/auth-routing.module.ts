import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { guestGuard } from '../../core/guards/auth.guards';
import { LoginComponent } from './login/login.component';
import { RegisterSuccessComponent } from './register-success/register-success.component';
import { RegisterComponent } from './register/register.component';

const routes: Routes = [
  { path: 'ingresar', component: LoginComponent, canMatch: [guestGuard], title: 'Ingresar · Kadree Bank' },
  { path: 'registro', component: RegisterComponent, canMatch: [guestGuard], title: 'Crear cuenta · Kadree Bank' },
  { path: 'registro/listo', component: RegisterSuccessComponent, title: 'Cuenta creada · Kadree Bank' },
  { path: '', pathMatch: 'full', redirectTo: 'ingresar' },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule],
})
export class AuthRoutingModule {}
