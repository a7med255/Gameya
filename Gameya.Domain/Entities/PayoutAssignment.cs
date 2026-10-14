using Gameya.Domain.Common;
using Gameya.Domain.Enums;
using Gameya.Domain.Events;
using Gameya.Domain.Rules;

namespace Gameya.Domain.Entities;

public sealed class PayoutAssignment : AggregateRoot
{
    private PayoutAssignment() { }

    internal PayoutAssignment(Guid associationId, Guid periodId, PayoutRecipientType recipientType,
        Guid? membershipId, decimal shareCount, DateTimeOffset now)
    {
        Guard.Against(shareCount <= 0 || decimal.Round(shareCount * 2, 0) != shareCount * 2,
            "payout.invalid_shares", "Payout share count must be positive and use half-share increments.");
        Guard.Against(recipientType == PayoutRecipientType.ReserveSlot && shareCount != 1,
            "payout.reserve_full_share", "An unnamed reserve slot must represent exactly one full share.");
        Guard.Against(recipientType == PayoutRecipientType.Membership && membershipId is null,
            "payout.membership_required", "A member payout requires a membership.");
        Guard.Against(recipientType == PayoutRecipientType.ReserveSlot && membershipId is not null,
            "payout.reserve_cannot_be_person", "An unnamed reserve slot cannot be modeled as a person.");
        AssociationId = associationId;
        PeriodId = periodId;
        RecipientType = recipientType;
        MembershipId = membershipId;
        ShareCount = shareCount;
        Status = PayoutStatus.Scheduled;
        CreatedAtUtc = now;
    }

    public Guid AssociationId { get; private set; }
    public Guid PeriodId { get; private set; }
    public PayoutRecipientType RecipientType { get; private set; }
    public Guid? MembershipId { get; private set; }
    public Guid? BeneficiaryPersonId { get; private set; }
    public decimal ShareCount { get; private set; }
    public PayoutStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal void Reschedule(Guid periodId)
    {
        Guard.Against(Status == PayoutStatus.Completed, "payout.historical_immutable", "A completed payout cannot be changed.");
        PeriodId = periodId;
    }

    internal void AssociateReserveBeneficiary(Guid personId)
    {
        Guard.Against(RecipientType != PayoutRecipientType.ReserveSlot, "payout.not_reserve", "Only a reserve payout can have a later beneficiary.");
        Guard.Against(Status == PayoutStatus.Completed, "payout.historical_immutable", "A completed payout cannot be changed.");
        BeneficiaryPersonId = personId;
    }

    public void Complete(DateTimeOffset now)
    {
        Guard.Against(Status == PayoutStatus.Completed, "payout.already_completed", "Payout is already completed.");
        Status = PayoutStatus.Completed;
        CompletedAtUtc = now;
        Raise(new PayoutCompleted(Id, AssociationId, now));
    }
}
