export type Role = 'Admin' | 'Owner' | 'PaymentUser' | 'Participant';
export type AssociationStatus = 'Draft' | 'Active' | 'Paused' | 'Completed' | 'Cancelled';
export type MembershipStatus = 'Active' | 'Exited' | 'Replaced';
export type MembershipKind = 'Paying' | 'Advance';
export type PeriodStatus = 'Upcoming' | 'Due' | 'Closed';
export type PayoutRecipientType = 'Membership' | 'ReserveSlot' | 'AdvanceSlot';
export type PayoutStatus = 'Scheduled' | 'Completed';
export type PaymentMethod = 'Cash' | 'InstaPay' | 'VodafoneCash';
export type PaymentEntryType = 'Payment' | 'Adjustment';
export type SettlementStatus = 'Pending' | 'Completed';

export interface PagedResult<T> {
  items: readonly T[];
  page: number;
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
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
  errors?: Readonly<Record<string, readonly string[]>> | readonly BusinessValidationError[];
}

export interface BusinessValidationError {
  code: string;
  field: string;
  periodNumber: number | null;
  message: string;
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
export interface AssociationEditorDto {
  association: AssociationDetailsDto;
  totalMemberShares: number;
  totalScheduledMemberShares: number;
  fullSharePayoutValue: number;
  totalMonthlyObligation: number;
  validationErrors: readonly BusinessValidationError[];
}

export interface MembershipDto {
  id: string;
  personId: string;
  personName: string;
  shareCount: number;
  kind: MembershipKind;
  status: MembershipStatus;
  displayOrder: number;
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
  amountDueNow: number;
  totalRemaining: number;
  oldestUnpaidDueDate: string | null;
  credit: number;
  paymentStatus: 'Paid' | 'Outstanding' | 'Upcoming';
}

export interface PaymentPageDto {
  personId: string;
  fullName: string;
  subscriberCode: string;
  associations: readonly AccessibleAssociationDueDto[];
  dueItems: readonly PaymentDueItemDto[];
  previousOverdue: number;
  currentDue: number;
  amountDueNow: number;
  totalRemaining: number;
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
export interface UpdatePersonRequest { fullName: string; phoneNumber: string; }
export interface CreateAssociationRequest { name: string; startDate: string; numberOfMonths: number; monthlyContribution: number; }
export interface AddMemberRequest { personId: string; shareCount: number; kind: MembershipKind; }
export interface AddPayoutRequest { periodId: string; recipientType: PayoutRecipientType; membershipId: string | null; shareCount: number; }
export interface AssociationEditorMemberInput {
  clientKey: string;
  personId: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  shareCount: number;
  displayOrder: number;
}
export interface AssociationEditorPayoutInput {
  periodNumber: number;
  recipientType: PayoutRecipientType;
  memberClientKey: string | null;
  shareCount: number;
  beneficiaryMemberClientKey: string | null;
}
export interface SaveAssociationConfigurationRequest extends CreateAssociationRequest {
  members: readonly AssociationEditorMemberInput[];
  payouts: readonly AssociationEditorPayoutInput[];
  startAssociation: boolean;
}
export interface MovePayoutRequest { periodId: string; reason: string | null; }
export interface ReplacementPersonInput { personId: string | null; fullName: string | null; phoneNumber: string | null; shareCount: number; }
export interface ReplaceMemberRequest { replacements: readonly ReplacementPersonInput[]; reason: string | null; }
export interface RecordPaymentRequest {
  personId: string;
  amount: number;
  method: PaymentMethod;
  idempotencyKey: string;
}

export interface PaymentDueItemDto {
  associationId: string;
  associationName: string;
  periodId: string;
  periodNumber: number;
  dueDate: string;
  amount: number;
}
export interface AdjustPaymentRequest {
  amount: number;
  reason: string;
  allocations: readonly { associationId: string; periodId: string; amount: number }[];
  credits: readonly { associationId: string; amount: number }[];
}

export interface PaymentListAllocationDto extends PaymentAllocationDto {
  associationName: string;
  periodNumber: number;
}

export interface PaymentListItemDto {
  id: string;
  personId: string;
  payerName: string;
  recordedByUserId: string;
  amount: number;
  method: PaymentMethod;
  paidAtUtc: string;
  entryType: PaymentEntryType;
  adjustsPaymentId: string | null;
  allocations: readonly PaymentListAllocationDto[];
  credits: readonly PaymentCreditDto[];
}

export interface PaymentFilters {
  fromDate?: string;
  toDate?: string;
  method?: PaymentMethod | '';
  personId?: string;
  associationId?: string;
  periodNumber?: number | null;
  entryType?: PaymentEntryType | '';
  payerSearch?: string;
}

export type PaymentUserInvitationStatus = 'Active' | 'Accepted' | 'Revoked' | 'Expired';
export interface PaymentUserDto {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  createdAtUtc: string;
  associationIds: readonly string[];
}
export interface PaymentUserInvitationDto {
  id: string;
  email: string;
  createdAtUtc: string;
  expiresAtUtc: string | null;
  acceptedAtUtc: string | null;
  revokedAtUtc: string | null;
  associationIds: readonly string[];
  status: PaymentUserInvitationStatus;
}
export interface InvitePaymentUserRequest { email: string; associationIds: readonly string[]; }
export interface SendReportResult { executionId: string; }
export interface CreatedId { id: string; }
