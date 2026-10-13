using Gameya.Domain.Common;

namespace Gameya.Domain.Events;

public sealed record AssociationStarted(Guid AssociationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record AssociationCompleted(Guid AssociationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record PaymentRecorded(Guid PaymentId, Guid PersonId, decimal Amount, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record PayoutCompleted(Guid PayoutId, Guid AssociationId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record MemberReplaced(Guid ReplacementId, Guid MembershipId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
public sealed record SettlementCreated(Guid SettlementId, Guid MembershipId, DateTimeOffset OccurredAtUtc) : IDomainEvent;
