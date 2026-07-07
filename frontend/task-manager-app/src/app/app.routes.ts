import { Routes } from '@angular/router';
import { authGuard } from './core/auth';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'boards',
    loadComponent: () => import('./features/boards/board-list.component').then((m) => m.BoardListComponent),
    canActivate: [authGuard],
  },
  {
    path: 'boards/:id',
    loadComponent: () => import('./features/boards/board-detail.component').then((m) => m.BoardDetailComponent),
    canActivate: [authGuard],
  },
  {
    path: 'analytics',
    loadComponent: () =>
      import('./features/analytics/analytics-dashboard.component').then((m) => m.AnalyticsDashboardComponent),
    canActivate: [authGuard],
  },
  {
    path: 'auth/callback',
    loadComponent: () =>
      import('./features/auth/auth-callback.component').then((m) => m.AuthCallbackComponent),
  },
  { path: '**', redirectTo: 'boards' },
];
