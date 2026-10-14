using Gameya.Domain.Common;
using Gameya.Domain.Enums;
using Gameya.Domain.Rules;

namespace Gameya.Domain.Entities;

public sealed class AssociationMembership : Entity
{
    private AssociationMembership() { }

    internal AssociationMembership(Guid associationId, Guid personId, decimal shareCount, MembershipKind kind,
        DateTimeOffset joinedAt, int displayOrder = 0)
    {
        Guard.Against(shareCount <= 0 || decimal.Round(shareCount * 2, 0) != shareCount * 2,
            "membership.invalid_shares", "Share count must be positive and use half-share increments.");
        AssociationId = associationId;
        PersonId = personId;
        ShareCount = shareCount;
        Kind = kind;
        DisplayOrder = displayOrder;
        Status = MembershipStatus.Active;
        JoinedAtUtc = joinedAt;
    }

    public Guid AssociationId { get; private set; }
    public Guid PersonId { get; private set; }
    public decimal ShareCount { get; private set; }
    public MembershipKind Kind { get; private set; }
    public int DisplayOrder { get; private set; }
    public MembershipStatus Status { get; private set; }
    public DateTimeOffset JoinedAtUtc { get; private set; }
    public DateTimeOffset? LeftAtUtc { get; private set; }

    internal void ChangeShares(decimal shareCount)
    {
        Guard.Against(shareCount <= 0 || decimal.Round(shareCount * 2, 0) != shareCount * 2,
            "membership.invalid_shares", "Share count must be positive and use half-share increments.");
        ShareCount = shareCount;
    }

    internal void MarkReplaced(DateTimeOffset at)
    {
        Guard.Against(Status != MembershipStatus.Active, "membership.not_active", "Only an active membership can be replaced.");
        Status = MembershipStatus.Replaced;
        LeftAtUtc = at;
    }
}
