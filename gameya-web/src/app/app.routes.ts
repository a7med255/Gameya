import { Routes } from '@angular/router';
import { associationAccessGuard } from './core/guards/association-access.guard';
import { authGuard } from './core/guards/auth.guard';
import { homeGuard } from './core/guards/home.guard';
import { roleGuard } from './core/guards/role.guard';
import { unsavedChangesGuard } from './core/guards/unsaved-changes.guard';

export const routes: Routes = [
  { path: '', pathMatch: 'full', canActivate: [homeGuard], children: [] },
  { path: 'auth/login', loadComponent: () => import('./features/auth/login.component').then((x) => x.LoginComponent) },
  { path: 'auth/register', loadComponent: () => import('./features/auth/register.component').then((x) => x.RegisterComponent) },
  { path: 'auth/confirm-email', loadComponent: () => import('./features/auth/email-confirmation.component').then((x) => x.EmailConfirmationComponent) },
  { path: 'participant/login', loadComponent: () => import('./features/auth/participant-login.component').then((x) => x.ParticipantLoginComponent) },
  { path: 'payment-user-invitation', loadComponent: () => import('./features/auth/accept-invitation.component').then((x) => x.AcceptInvitationComponent) },
  {
    path: 'owner', canActivate: [authGuard, roleGuard], data: { roles: ['Admin', 'Owner'] },
    loadComponent: () => import('./layout/app-shell/app-shell.component').then((x) => x.AppShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      { path: 'dashboard', loadComponent: () => import('./features/dashboard/dashboard.component').then((x) => x.DashboardComponent) },
      { path: 'people', loadComponent: () => import('./features/people/people-list.component').then((x) => x.PeopleListComponent) },
      { path: 'people/:id', loadComponent: () => import('./features/people/person-profile.component').then((x) => x.PersonProfileComponent) },
      { path: 'associations', loadComponent: () => import('./features/associations/associations-list.component').then((x) => x.AssociationsListComponent) },
      { path: 'associations/new', canDeactivate: [unsavedChangesGuard], loadComponent: () => import('./features/associations/create-association.component').then((x) => x.CreateAssociationComponent) },
      { path: 'associations/:id', canActivate: [associationAccessGuard], loadComponent: () => import('./features/associations/association-details.component').then((x) => x.AssociationDetailsComponent) },
      { path: 'payments', loadComponent: () => import('./features/payments/payments-history.component').then((x) => x.PaymentsHistoryComponent) },
      { path: 'payments/new', loadComponent: () => import('./features/payments/payment-workflow.component').then((x) => x.PaymentWorkflowComponent) },
      { path: 'replacements', data: { mode: 'replacements' }, loadComponent: () => import('./features/workflows/workflow-directory.component').then((x) => x.WorkflowDirectoryComponent) },
      { path: 'settlements', data: { mode: 'settlements' }, loadComponent: () => import('./features/workflows/workflow-directory.component').then((x) => x.WorkflowDirectoryComponent) },
      { path: 'reports', loadComponent: () => import('./features/reports/reports.component').then((x) => x.ReportsComponent) },
      { path: 'settings', loadComponent: () => import('./features/settings/settings.component').then((x) => x.SettingsComponent) },
    ],
  },
  {
    path: 'payment', canActivate: [authGuard, roleGuard], data: { roles: ['PaymentUser'] },
    loadComponent: () => import('./layout/app-shell/app-shell.component').then((x) => x.AppShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'pay' },
      { path: 'pay', loadComponent: () => import('./features/payments/payment-workflow.component').then((x) => x.PaymentWorkflowComponent) },
      { path: 'history', loadComponent: () => import('./features/payments/payments-history.component').then((x) => x.PaymentsHistoryComponent) },
    ],
  },
  {
    path: 'participant', canActivate: [authGuard, roleGuard], data: { roles: ['Participant'] },
    loadComponent: () => import('./layout/participant-layout/participant-layout.component').then((x) => x.ParticipantLayoutComponent),
    children: [{ path: '', loadComponent: () => import('./features/participant/participant-portal.component').then((x) => x.ParticipantPortalComponent) }],
  },
  { path: 'admin', redirectTo: 'owner/dashboard', pathMatch: 'full' },
  { path: 'not-found', loadComponent: () => import('./features/not-found.component').then((x) => x.NotFoundComponent) },
  { path: '**', redirectTo: 'not-found' },
];
