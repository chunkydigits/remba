import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

const routes: Routes = [
  {
    path: '',
    loadChildren: () => import('./modules/home/home.module').then((m) => m.HomeModule),
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadChildren: () => import('./modules/account/account.module').then((m) => m.AccountModule),
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadChildren: () => import('./modules/settings/settings.module').then((m) => m.SettingsModule),
  },
  {
    path: '**',
    redirectTo: '',
  },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule],
})
export class AppRoutingModule {}
