import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadChildren: () => import('./modules/home/home.routes').then((m) => m.HOME_ROUTES),
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadChildren: () => import('./modules/account/account.routes').then((m) => m.ACCOUNT_ROUTES),
  },
  {
    path: 'settings',
    canActivate: [authGuard],
    loadChildren: () => import('./modules/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
  },
  {
    path: 'lists',
    canActivate: [authGuard],
    loadChildren: () => import('./modules/lists/lists.routes').then((m) => m.LISTS_ROUTES),
  },
  {
    path: 'developer',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./modules/developer/pages/developer/developer.component').then(
        (m) => m.DeveloperComponent,
      ),
  },
  {
    path: 'save',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./modules/lists/pages/save-from-plugin/save-from-plugin.component').then(
        (m) => m.SaveFromPluginComponent,
      ),
  },
  {
    path: '**',
    redirectTo: '',
  },
];
