using Gameya.Domain.Enums;

namespace Gameya.Application.DTOs;

public sealed record AuthTokensDto(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken);
public sealed record RegistrationResultDto(string UserId, bool RequiresEmailConfirmation);
public sealed record GeneratedReportDto(byte[] Content, string ContentType, string FileName);
public sealed record PaymentUserDto(string Id, string Email, string DisplayName, bool IsActive,
    DateTimeOffset CreatedAtUtc, IReadOnlyCollection<Guid> AssociationIds);
public sealed record PaymentUserInvitationDto(Guid Id, string Email, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ExpiresAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? RevokedAtUtc,
    IReadOnlyCollection<Guid> AssociationIds, string Status);

public sealed record PersonDto(Guid Id, string FullName, string PhoneNumber, string SubscriberCode, bool IsActive,
    DateTimeOffset CreatedAtUtc);
public sealed record PersonProfileDto(PersonDto Person, IReadOnlyCollection<PersonMembershipDto> Memberships,
    IReadOnlyCollection<PayoutDto> Payouts, IReadOnlyCollection<PaymentDto> Payments, IReadOnlyCollection<SettlementDto> Settlements,
    decimal TotalObligations, decimal TotalPaid, decimal TotalRemaining);
public sealed record PersonMembershipDto(Guid MembershipId, Guid AssociationId, string AssociationName,
    MembershipStatus Status, MembershipKind Kind, decimal ShareCount, decimal Paid, decimal Remaining);

public sealed record AssociationSummaryDto(Guid Id, string Name, DateOnly StartDate, DateOnly? EndDate,
    int NumberOfMonths, decimal MonthlyContribution, AssociationStatus Status, int MemberCount);
public sealed record AssociationDetailsDto(Guid Id, string Name, DateOnly StartDate, DateOnly? EndDate,
    int NumberOfMonths, decimal MonthlyContribution, AssociationStatus Status, string OwnerId,
    IReadOnlyCollection<MembershipDto> Memberships, IReadOnlyCollection<PeriodDto> Periods,
    IReadOnlyCollection<PayoutDto> Payouts);
public sealed record MembershipDto(Guid Id, Guid PersonId, string PersonName, decimal ShareCount,
    MembershipKind Kind, MembershipStatus Status, int DisplayOrder, DateTimeOffset JoinedAtUtc,
    DateTimeOffset? LeftAtUtc);
public sealed record PeriodDto(Guid Id, int PeriodNumber, DateOnly PeriodDate, DateOnly DueDate, PeriodStatus Status);
public sealed record PayoutDto(Guid Id, Guid PeriodId, int PeriodNumber, PayoutRecipientType RecipientType,
    Guid? MembershipId, Guid? BeneficiaryPersonId, decimal ShareCount, decimal Value, PayoutStatus Status);
public sealed record AssociationEditorValidationDto(string Code, string? Field, int? PeriodNumber, string Message);
public sealed record AssociationEditorDto(AssociationDetailsDto Association, decimal TotalMemberShares,
    decimal TotalScheduledMemberShares, decimal FullSharePayoutValue, decimal TotalMonthlyObligation,
    IReadOnlyCollection<AssociationEditorValidationDto> ValidationErrors);

public sealed record PaymentDto(Guid Id, Guid PersonId, decimal Amount, PaymentMethod Method, DateTimeOffset PaidAtUtc,
    PaymentEntryType EntryType, IReadOnlyCollection<PaymentAllocationDto> Allocations, IReadOnlyCollection<PaymentCreditDto> Credits);
public sealed record PaymentAllocationDto(Guid AssociationId, Guid PeriodId, decimal Amount, DateOnly DueDate, int LateDays);
public sealed record PaymentCreditDto(Guid AssociationId, decimal Amount);
public sealed record PaymentListAllocationDto(Guid AssociationId, string AssociationName, Guid PeriodId,
    int PeriodNumber, decimal Amount, DateOnly DueDate, int LateDays);
public sealed record PaymentListItemDto(Guid Id, Guid PersonId, string PayerName, string RecordedByUserId,
    decimal Amount, PaymentMethod Method, DateTimeOffset PaidAtUtc, PaymentEntryType EntryType,
    Guid? AdjustsPaymentId, IReadOnlyCollection<PaymentListAllocationDto> Allocations,
    IReadOnlyCollection<PaymentCreditDto> Credits);
public sealed record PaymentDueItemDto(Guid AssociationId, string AssociationName, Guid PeriodId,
    int PeriodNumber, DateOnly PeriodDate, DateOnly DueDate, decimal Required, decimal Paid,
    decimal Amount, string PaymentStatus);
public sealed record PaymentMonthDueDto(int Year, int Month, string MonthLabel, decimal Required,
    decimal Paid, decimal Remaining, string PaymentStatus, IReadOnlyCollection<PaymentDueItemDto> Items);
public sealed record PaymentPageDto(Guid PersonId, string FullName, string SubscriberCode,
    IReadOnlyCollection<AccessibleAssociationDueDto> Associations, IReadOnlyCollection<PaymentDueItemDto> DueItems,
    int CurrentYear, int CurrentMonthNumber, string CurrentMonthLabel,
    IReadOnlyCollection<PaymentMonthDueDto> ArrearMonths, PaymentMonthDueDto CurrentMonth,
    PaymentMonthDueDto? NextAdvanceMonth, decimal PreviousOverdue, decimal CurrentDue,
    decimal AmountDueNow, decimal TotalRemaining);
public sealed record AccessibleAssociationDueDto(Guid AssociationId, string AssociationName,
    AssociationStatus AssociationStatus, decimal ShareCount, decimal MonthlyContribution,
    int? CurrentPeriodNumber, decimal CurrentRequired, decimal CurrentPaid, decimal CurrentRemaining,
    decimal PreviousArrears, decimal AmountDueNow, decimal TotalRemaining, DateOnly? OldestUnpaidDueDate,
    decimal Credit, string PaymentStatus);
public sealed record PaymentPreviewAllocationDto(Guid AssociationId, string AssociationName, Guid PeriodId,
    int PeriodNumber, DateOnly PeriodDate, DateOnly DueDate, string Category, decimal Required,
    decimal PaidBefore, decimal Allocated, decimal RemainingAfter, string PaymentStatus);
public sealed record PaymentPreviewCreditDto(Guid AssociationId, string AssociationName, decimal Amount);
public sealed record PaymentPreviewDto(Guid PersonId, decimal AmountReceived, decimal AllocatedAmount,
    decimal CreditAmount, bool WasIntentReallocated, string? AllocationNotice, string PreviewToken,
    IReadOnlyCollection<PaymentPreviewAllocationDto> Allocations,
    IReadOnlyCollection<PaymentPreviewCreditDto> Credits);
public sealed record SettlementDto(Guid Id, Guid AssociationId, Guid MembershipId, decimal TotalRequired,
    decimal TotalPaid, decimal Balance, SettlementStatus Status);

public sealed record MonthlyAssociationAmountDto(Guid AssociationId, string AssociationName, Guid PeriodId,
    int PeriodNumber, DateOnly DueDate, decimal Required, decimal Paid, decimal Remaining, decimal Overdue);
public sealed record MonthlyPayoutAmountDto(Guid AssociationId, string AssociationName, Guid PeriodId,
    int PeriodNumber, decimal Amount);
public sealed record MonthlyCollectionPersonDto(Guid PersonId, string PersonName, decimal Required, decimal Paid,
    decimal Remaining, decimal Overdue, decimal ScheduledPayouts, decimal NetPayout, decimal AmountOwed,
    IReadOnlyCollection<MonthlyAssociationAmountDto> Associations,
    IReadOnlyCollection<MonthlyPayoutAmountDto> Payouts);
public sealed record MonthlyPayoutPersonDto(Guid PersonId, string PersonName, decimal ScheduledPayouts,
    decimal ContributionsDue, decimal HistoricalOverdue, decimal NetPayout, decimal AmountOwed,
    IReadOnlyCollection<MonthlyPayoutAmountDto> Payouts,
    IReadOnlyCollection<MonthlyAssociationAmountDto> Contributions);
public sealed record MonthlyFinanceDto(int Year, int Month, string MonthLabel, decimal TotalRequired,
    decimal TotalPaid, decimal TotalRemaining, decimal TotalOverdue, int OverduePeopleCount,
    decimal CollectionProgress, int ActiveAssociations, int TotalPeople, int ScheduledPayoutCount,
    IReadOnlyCollection<MonthlyCollectionPersonDto> Collections,
    IReadOnlyCollection<MonthlyPayoutPersonDto> PayoutRecipients);
public sealed record DashboardDto(int Year, int Month, string MonthLabel, decimal TotalDue, decimal TotalPaid,
    decimal TotalRemaining, decimal TotalOverdue, int OverduePeopleCount, decimal CollectionProgress,
    int ActiveAssociations, int TotalPeople, int CurrentPayouts);
public sealed record AssociationAuditDto(Guid Id, string Action, string EntityName, string EntityId,
    string? UserId, string ActorName, string? OldValues, string? NewValues, DateTimeOffset TimestampUtc);
