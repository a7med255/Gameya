using Gameya.Domain.Common;
using Gameya.Domain.Events;

namespace Gameya.Domain.Entities;

public sealed class Replacement : AggregateRoot
{
    private readonly List<ReplacementShare> _shares = [];
    private Replacement() { }

    internal Replacement(Guid associationId, Guid oldMembershipId, IEnumerable<ReplacementShare> shares,
        string? reason, DateTimeOffset now)
    {
        AssociationId = associationId;
        OldMembershipId = oldMembershipId;
        _shares.AddRange(shares);
        Reason = reason;
        OccurredAtUtc = now;
        Raise(new MemberReplaced(Id, oldMembershipId, now));
    }

    public Guid AssociationId { get; private set; }
    public Guid OldMembershipId { get; private set; }
    public string? Reason { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public IReadOnlyCollection<ReplacementShare> Shares => _shares.AsReadOnly();
}

public sealed class ReplacementShare : Entity
{
    private ReplacementShare() { }
    public ReplacementShare(Guid newMembershipId, decimal shareCount)
    {
        NewMembershipId = newMembershipId;
        ShareCount = shareCount;
    }
    public Guid ReplacementId { get; private set; }
    public Guid NewMembershipId { get; private set; }
    public decimal ShareCount { get; private set; }
}
