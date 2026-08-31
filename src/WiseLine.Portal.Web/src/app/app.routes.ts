import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';

export const routes: Routes = [
  {
    path: '',
    title: 'WiseLine Trade — AI-ready investing context',
    loadComponent: () => import('./pages/landing/landing').then((m) => m.LandingPage),
  },
  {
    path: 'pricing',
    title: 'Pricing — WiseLine Trade',
    loadComponent: () => import('./pages/pricing/pricing').then((m) => m.PricingPage),
  },
  {
    path: 'docs',
    title: 'Connection guide — WiseLine Trade',
    loadComponent: () => import('./pages/docs/docs').then((m) => m.DocsPage),
  },
  {
    path: 'login',
    title: 'Log in — WiseLine Trade',
    loadComponent: () => import('./pages/login/login').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    title: 'Start your free trial — WiseLine Trade',
    loadComponent: () => import('./pages/register/register').then((m) => m.RegisterPage),
  },
  {
    path: 'dashboard',
    title: 'Dashboard — WiseLine Trade',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/dashboard/dashboard').then((m) => m.DashboardPage),
  },
  {
    path: 'portfolios',
    title: 'Portfolios — WiseLine Trade',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/portfolios/portfolios').then((m) => m.PortfoliosPage),
  },
  {
    path: 'portfolios/:id',
    title: 'Portfolio — WiseLine Trade',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/portfolio-detail/portfolio-detail').then((m) => m.PortfolioDetailPage),
  },
  {
    path: 'mcp-access',
    title: 'MCP access — WiseLine Trade',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/mcp-access/mcp-access').then((m) => m.McpAccessPage),
  },
  {
    path: 'account',
    title: 'Account — WiseLine Trade',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/account/account').then((m) => m.AccountPage),
  },
  { path: '**', redirectTo: '' },
];
