export type Role = 'Admin' | 'Owner' | 'PaymentUser' | 'Participant';
export type AssociationStatus = 'Draft' | 'Active' | 'Paused' | 'Completed' | 'Cancelled';
export type MembershipStatus = 'Active' | 'Exited' | 'Replaced';
export type MembershipKind = 'Paying' | 'Advance';
export type PeriodStatus = 'Upcoming' | 'Due' | 'Closed';
export type PayoutRecipientType = 'Membership' | 'ReserveSlot';
export type PayoutStatus = 'Scheduled' | 'Completed';
export type PaymentMethod = 'Cash' | 'InstaPay' | 'VodafoneCash';
export type PaymentEntryType = 'Payment' | 'Adjustment';
export type SettlementStatus = 'Pending' | 'Completed';

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuthTokensDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
}

export interface RegistrationResultDto {
  userId: string;
  requiresEmailConfirmation: boolean;
}

export interface CurrentUser {
  subject: string;
  displayName: string;
  email: string | null;
  personId: string | null;
  roles: readonly Role[];
}

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  errors?: Readonly<Record<string, readonly string[]>>;
}

export interface PersonDto {
  id: string;
  fullName: string;
  phoneNumber: string;
  subscriberCode: string;
  isActive: boolean;
  createdAtUtc: string;
}

export interface PersonMembershipDto {
  membershipId: string;
  associationId: string;
  associationName: string;
  status: MembershipStatus;
  kind: MembershipKind;
  shareCount: number;
  paid: number;
  remaining: number;
}

export interface PersonProfileDto {
  person: PersonDto;
  memberships: readonly PersonMembershipDto[];
  payouts: readonly PayoutDto[];
  payments: readonly PaymentDto[];
  settlements: readonly SettlementDto[];
  totalObligations: number;
  totalPaid: number;
  totalRemaining: number;
}

export interface AssociationSummaryDto {
  id: string;
  name: string;
  startDate: string;
  endDate: string | null;
  numberOfMonths: number;
  monthlyContribution: number;
  status: AssociationStatus;
  memberCount: number;
}

export interface AssociationDetailsDto {
  id: string;
  name: string;
  startDate: string;
  endDate: string | null;
  numberOfMonths: number;
  monthlyContribution: number;
  status: AssociationStatus;
  ownerId: string;
  memberships: readonly MembershipDto[];
  periods: readonly PeriodDto[];
  payouts: readonly PayoutDto[];
}

export interface MembershipDto {
  id: string;
  personId: string;
  personName: string;
  shareCount: number;
  kind: MembershipKind;
  status: MembershipStatus;
  joinedAtUtc: string;
  leftAtUtc: string | null;
}

export interface PeriodDto {
  id: string;
  periodNumber: number;
  periodDate: string;
  dueDate: string;
  status: PeriodStatus;
}

export interface PayoutDto {
  id: string;
  periodId: string;
  periodNumber: number;
  recipientType: PayoutRecipientType;
  membershipId: string | null;
  beneficiaryPersonId: string | null;
  shareCount: number;
  value: number;
  status: PayoutStatus;
}

export interface PaymentAllocationDto {
  associationId: string;
  periodId: string;
  amount: number;
  dueDate: string;
  lateDays: number;
}

export interface PaymentCreditDto {
  associationId: string;
  amount: number;
}

export interface PaymentDto {
  id: string;
  personId: string;
  amount: number;
  method: PaymentMethod;
  paidAtUtc: string;
  entryType: PaymentEntryType;
  allocations: readonly PaymentAllocationDto[];
  credits: readonly PaymentCreditDto[];
}

export interface AccessibleAssociationDueDto {
  associationId: string;
  associationName: string;
  monthlyContribution: number;
  outstandingAmount: number;
  oldestUnpaidDueDate: string | null;
  credit: number;
  paymentStatus: 'Paid' | 'Outstanding' | 'Upcoming';
}

export interface PaymentPageDto {
  personId: string;
  fullName: string;
  subscriberCode: string;
  associations: readonly AccessibleAssociationDueDto[];
  totalOutstanding: number;
}

export interface SettlementDto {
  id: string;
  associationId: string;
  membershipId: string;
  totalRequired: number;
  totalPaid: number;
  balance: number;
  status: SettlementStatus;
}

export interface DashboardDto {
  totalDue: number;
  totalPaid: number;
  totalRemaining: number;
  activeAssociations: number;
  totalPeople: number;
  currentPayouts: number;
  outstandingPayments: number;
}

export interface RegisterRequest { name: string; email: string; password: string; }
export interface LoginRequest { email: string; password: string; }
export interface ParticipantLoginRequest { phoneNumber: string; subscriberCode: string; }
export interface CreatePersonRequest { fullName: string; phoneNumber: string; }
export interface CreateAssociationRequest { name: string; startDate: string; numberOfMonths: number; monthlyContribution: number; }
export interface AddMemberRequest { personId: string; shareCount: number; kind: MembershipKind; }
export interface AddPayoutRequest { periodId: string; recipientType: PayoutRecipientType; membershipId: string | null; shareCount: number; }
export interface MovePayoutRequest { periodId: string; reason: string | null; }
export interface ReplacementPersonInput { personId: string | null; fullName: string | null; phoneNumber: string | null; shareCount: number; }
export interface ReplaceMemberRequest { replacements: readonly ReplacementPersonInput[]; reason: string | null; }
export interface RecordPaymentRequest {
  personId: string;
  amount: number;
  method: PaymentMethod;
  associationIds: readonly string[] | null;
  creditAssociationId: string | null;
}
export interface InvitePaymentUserRequest { email: string; associationIds: readonly string[]; }
export interface SendReportResult { jobId: string; }
export interface CreatedId { id: string; }
