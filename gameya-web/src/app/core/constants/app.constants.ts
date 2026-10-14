import { AssociationStatus, MembershipKind, MembershipStatus, PaymentMethod, PayoutStatus, Role } from '../models/api.models';

export const ROLES = {
  admin: 'Admin',
  owner: 'Owner',
  paymentUser: 'PaymentUser',
  participant: 'Participant',
} as const satisfies Record<string, Role>;

export const ROLE_HOME: Readonly<Record<Role, string>> = {
  Admin: '/owner/dashboard',
  Owner: '/owner/dashboard',
  PaymentUser: '/payment/pay',
  Participant: '/participant',
};

export const ASSOCIATION_STATUS_LABELS: Readonly<Record<AssociationStatus, string>> = {
  Draft: 'مسودة',
  Active: 'نشطة',
  Paused: 'متوقفة مؤقتًا',
  Completed: 'مكتملة',
  Cancelled: 'ملغاة',
};

export const MEMBERSHIP_STATUS_LABELS: Readonly<Record<MembershipStatus, string>> = {
  Active: 'نشط',
  Exited: 'خرج من الجمعية',
  Replaced: 'تم استبداله',
};

export const MEMBERSHIP_KIND_LABELS: Readonly<Record<MembershipKind, string>> = {
  Paying: 'عضو دافع',
  Advance: 'سلف',
};

export const PAYOUT_STATUS_LABELS: Readonly<Record<PayoutStatus, string>> = {
  Scheduled: 'مجدول',
  Completed: 'تم القبض',
};

export const PAYMENT_METHOD_LABELS: Readonly<Record<PaymentMethod, string>> = {
  Cash: 'كاش',
  InstaPay: 'إنستا باي',
  VodafoneCash: 'فودافون كاش',
};

export const PAYMENT_METHODS: readonly PaymentMethod[] = ['Cash', 'InstaPay', 'VodafoneCash'];
export const SHARE_OPTIONS = [0.5, 1, 1.5, 2, 2.5, 3] as const;
