using Gameya.Domain.Common;
using Gameya.Domain.Enums;
using Gameya.Domain.Events;
using Gameya.Domain.Rules;

namespace Gameya.Domain.Entities;

public sealed class Settlement : AggregateRoot
{
    private Settlement() { }
    public Settlement(Guid associationId, Guid membershipId, decimal totalRequired, decimal totalPaid, DateTimeOffset now)
    {
        Guard.Against(totalRequired < 0 || totalPaid < 0, "settlement.invalid_totals", "Settlement totals cannot be negative.");
        AssociationId = associationId;
        MembershipId = membershipId;
        TotalRequired = totalRequired;
        TotalPaid = totalPaid;
        Balance = totalPaid - totalRequired;
        Status = SettlementStatus.Pending;
        CreatedAtUtc = now;
        Raise(new SettlementCreated(Id, membershipId, now));
    }
    public Guid AssociationId { get; private set; }
    public Guid MembershipId { get; private set; }
    public decimal TotalRequired { get; private set; }
    public decimal TotalPaid { get; private set; }
    public decimal Balance { get; private set; }
    public SettlementStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
}
