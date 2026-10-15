using Gameya.Domain.Enums;

namespace Gameya.Application.DTOs;

public sealed record AuthTokensDto(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, string RefreshToken);
public sealed record RegistrationResultDto(string UserId, bool RequiresEmailConfirmation);
public sealed record GeneratedReportDto(byte[] Content, string ContentType, string FileName);
public sealed record PaymentUserDto(string Id, string Email, string DisplayName, bool IsActive,
    DateTimeOffset CreatedAtUtc, IReadOnlyCollection<Guid> AssociationIds);
public sealed record PaymentUserInvitationDto(Guid Id, string Email, DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc, DateTimeOffset? AcceptedAtUtc, DateTimeOffset? RevokedAtUtc,
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
public sealed record PaymentPageDto(Guid PersonId, string FullName, string SubscriberCode,
    IReadOnlyCollection<AccessibleAssociationDueDto> Associations, decimal AmountDueNow, decimal TotalRemaining);
public sealed record AccessibleAssociationDueDto(Guid AssociationId, string AssociationName, decimal MonthlyContribution,
    decimal AmountDueNow, decimal TotalRemaining, DateOnly? OldestUnpaidDueDate, decimal Credit, string PaymentStatus);
public sealed record SettlementDto(Guid Id, Guid AssociationId, Guid MembershipId, decimal TotalRequired,
    decimal TotalPaid, decimal Balance, SettlementStatus Status);

public sealed record DashboardDto(decimal TotalDue, decimal TotalPaid, decimal TotalRemaining,
    int ActiveAssociations, int TotalPeople, int CurrentPayouts, int OutstandingPayments);
