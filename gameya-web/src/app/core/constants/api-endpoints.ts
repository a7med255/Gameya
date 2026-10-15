const root = {
  auth: '/auth',
  people: '/people',
  associations: '/associations',
  payments: '/payments',
  dashboard: '/dashboard',
  participant: '/participant',
  reports: '/reports/monthly',
  paymentUsers: '/payment-users',
} as const;

export const API_ENDPOINTS = {
  ...root,
  authLogin: `${root.auth}/login`,
  authRegister: `${root.auth}/register`,
  participantLogin: `${root.auth}/participant-login`,
  refresh: `${root.auth}/refresh`,
  logout: `${root.auth}/logout`,
  resendConfirmation: `${root.auth}/confirm-email/resend`,
  confirmEmail: `${root.auth}/confirm-email`,
  acceptInvitation: `${root.auth}/invitations/accept`,
  requestPasswordReset: `${root.auth}/password-reset/request`,
  resetPassword: `${root.auth}/password-reset`,
  participantMe: `${root.participant}/me`,
  sendMonthlyReport: `${root.reports}/send`,
} as const;
