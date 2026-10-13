using Gameya.Domain.Common;
using Gameya.Domain.Enums;
using Gameya.Domain.Events;
using Gameya.Domain.Rules;

namespace Gameya.Domain.Entities;

public sealed class Association : AggregateRoot
{
    private readonly List<AssociationMembership> _memberships = [];
    private readonly List<AssociationPeriod> _periods = [];
    private readonly List<PayoutAssignment> _payouts = [];
    private readonly List<Replacement> _replacements = [];
    private Association() { }

    public Association(string name, DateOnly startDate, int numberOfMonths, decimal monthlyContribution,
        string ownerId, DateTimeOffset now)
    {
        Guard.Against(string.IsNullOrWhiteSpace(name), "association.name_required", "Association name is required.");
        Guard.Against(numberOfMonths <= 0, "association.invalid_months", "Number of months must be positive.");
        Guard.Against(monthlyContribution <= 0, "association.invalid_contribution", "Monthly contribution must be positive.");
        Guard.Against(string.IsNullOrWhiteSpace(ownerId), "association.owner_required", "Owner is required.");
        Name = name.Trim();
        StartDate = startDate;
        NumberOfMonths = numberOfMonths;
        MonthlyContribution = monthlyContribution;
        OwnerId = ownerId;
        Status = AssociationStatus.Draft;
        CreatedAtUtc = now;
    }

    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public int NumberOfMonths { get; private set; }
    public decimal MonthlyContribution { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public AssociationStatus Status { get; private set; }
    public string OwnerId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<AssociationMembership> Memberships => _memberships.AsReadOnly();
    public IReadOnlyCollection<AssociationPeriod> Periods => _periods.AsReadOnly();
    public IReadOnlyCollection<PayoutAssignment> Payouts => _payouts.AsReadOnly();
    public IReadOnlyCollection<Replacement> Replacements => _replacements.AsReadOnly();
    public decimal FullSharePayoutValue => MonthlyContribution * NumberOfMonths;

    public AssociationMembership AddMember(Guid personId, decimal shareCount, MembershipKind kind, DateTimeOffset now)
    {
        Guard.Against(Status != AssociationStatus.Draft, "association.members_after_start", "Normal members can only be added while the association is in draft.");
        Guard.Against(_memberships.Any(x => x.PersonId == personId && x.Status == MembershipStatus.Active),
            "membership.duplicate", "This person is already an active member.");
        var membership = new AssociationMembership(Id, personId, shareCount, kind, now);
        _memberships.Add(membership);
        UpdatedAtUtc = now;
        return membership;
    }

    public void ChangeMemberShares(Guid membershipId, decimal shareCount, DateTimeOffset now)
    {
        Guard.Against(Status != AssociationStatus.Draft, "membership.shares_immutable", "Shares cannot change after the association starts.");
        var member = _memberships.SingleOrDefault(x => x.Id == membershipId);
        Guard.Against(member is null, "membership.not_found", "Membership was not found.");
        member!.ChangeShares(shareCount);
        UpdatedAtUtc = now;
    }

    public void SetPeriods(IEnumerable<(DateOnly PeriodDate, DateOnly DueDate)> dates)
    {
        Guard.Against(Status != AssociationStatus.Draft, "association.periods_immutable", "Periods can only be generated while the association is in draft.");
        var values = dates.ToList();
        Guard.Against(values.Count != NumberOfMonths, "association.period_count", "The period count must equal the configured number of months.");
        _periods.Clear();
        for (var i = 0; i < values.Count; i++)
            _periods.Add(new AssociationPeriod(Id, i + 1, values[i].PeriodDate, values[i].DueDate));
        EndDate = values[^1].PeriodDate;
    }

    public PayoutAssignment AddPayout(Guid periodId, PayoutRecipientType type, Guid? membershipId,
        decimal shareCount, DateTimeOffset now)
    {
        Guard.Against(Status is AssociationStatus.Completed or AssociationStatus.Cancelled,
            "association.closed", "A closed association cannot receive payout assignments.");
        Guard.Against(_periods.All(x => x.Id != periodId), "period.not_found", "Period was not found.");
        if (membershipId is not null)
            Guard.Against(_memberships.All(x => x.Id != membershipId), "membership.not_found", "Membership was not found.");
        var payout = new PayoutAssignment(Id, periodId, type, membershipId, shareCount, now);
        _payouts.Add(payout);
        return payout;
    }

    public void MoveFuturePayout(Guid payoutId, Guid periodId, DateOnly today, DateTimeOffset now)
    {
        var payout = _payouts.SingleOrDefault(x => x.Id == payoutId);
        Guard.Against(payout is null, "payout.not_found", "Payout was not found.");
        var period = _periods.SingleOrDefault(x => x.Id == periodId);
        Guard.Against(period is null, "period.not_found", "Period was not found.");
        Guard.Against(period!.PeriodDate < today, "payout.future_only", "Only future payout assignments can be changed.");
        payout!.Reschedule(periodId);
        UpdatedAtUtc = now;
    }

    public void RemoveFuturePayout(Guid payoutId, DateOnly today, DateTimeOffset now)
    {
        var payout = _payouts.SingleOrDefault(x => x.Id == payoutId);
        Guard.Against(payout is null, "payout.not_found", "Payout was not found.");
        var period = _periods.Single(x => x.Id == payout!.PeriodId);
        Guard.Against(payout!.Status == PayoutStatus.Completed || period.PeriodDate < today,
            "payout.historical_immutable", "A historical payout cannot be removed.");
        _payouts.Remove(payout);
        UpdatedAtUtc = now;
    }

    public void AssociateReservePayoutBeneficiary(Guid payoutId, Guid personId, DateTimeOffset now)
    {
        var payout = _payouts.SingleOrDefault(x => x.Id == payoutId);
        Guard.Against(payout is null, "payout.not_found", "Payout was not found.");
        payout!.AssociateReserveBeneficiary(personId);
        UpdatedAtUtc = now;
    }

    public void Start(DateTimeOffset now)
    {
        Guard.Against(Status != AssociationStatus.Draft, "association.start_from_draft", "Only a draft association can start.");
        Guard.Against(_periods.Count != NumberOfMonths, "association.periods_required", "All association periods must exist before starting.");
        Guard.Against(!_memberships.Any(x => x.Kind == MembershipKind.Paying && x.Status == MembershipStatus.Active),
            "association.members_required", "At least one paying member is required.");
        ValidateFinancialBalance();
        Status = AssociationStatus.Active;
        UpdatedAtUtc = now;
        Raise(new AssociationStarted(Id, now));
    }

    public void Pause(DateTimeOffset now)
    {
        Guard.Against(Status != AssociationStatus.Active, "association.pause_from_active", "Only an active association can be paused.");
        Status = AssociationStatus.Paused;
        UpdatedAtUtc = now;
    }

    public void ClosePeriod(Guid periodId, DateTimeOffset now)
    {
        Guard.Against(Status is not (AssociationStatus.Active or AssociationStatus.Paused),
            "association.period_close_state", "Periods can only close after the association starts.");
        var period = _periods.SingleOrDefault(x => x.Id == periodId);
        Guard.Against(period is null, "period.not_found", "Period was not found.");
        period!.Close();
        UpdatedAtUtc = now;
    }

    public void Complete(DateTimeOffset now)
    {
        Guard.Against(Status is not (AssociationStatus.Active or AssociationStatus.Paused),
            "association.complete_state", "Only an active or paused association can be completed.");
        Guard.Against(_periods.Any(x => x.Status != PeriodStatus.Closed),
            "association.open_periods", "All periods must be closed before completion.");
        Status = AssociationStatus.Completed;
        UpdatedAtUtc = now;
        Raise(new AssociationCompleted(Id, now));
    }

    public Replacement ReplaceMember(Guid oldMembershipId,
        IReadOnlyCollection<(Guid PersonId, decimal ShareCount)> replacements, string? reason, DateTimeOffset now)
    {
        Guard.Against(Status is not (AssociationStatus.Active or AssociationStatus.Paused),
            "replacement.after_start", "Replacement is available only after an association starts.");
        var old = _memberships.SingleOrDefault(x => x.Id == oldMembershipId);
        Guard.Against(old is null, "membership.not_found", "Membership was not found.");
        Guard.Against(replacements.Count == 0, "replacement.required", "At least one replacement is required.");
        Guard.Against(replacements.Sum(x => x.ShareCount) != old!.ShareCount,
            "replacement.share_mismatch", "Replacement shares must equal the old member's shares.");

        old.MarkReplaced(now);
        var links = new List<ReplacementShare>();
        foreach (var item in replacements)
        {
            var membership = new AssociationMembership(Id, item.PersonId, item.ShareCount, MembershipKind.Paying, now);
            _memberships.Add(membership);
            links.Add(new ReplacementShare(membership.Id, item.ShareCount));
        }
        var replacement = new Replacement(Id, old.Id, links, reason, now);
        _replacements.Add(replacement);
        UpdatedAtUtc = now;
        return replacement;
    }

    private void ValidateFinancialBalance()
    {
        var payingShares = _memberships
            .Where(x => x.Kind == MembershipKind.Paying && x.Status == MembershipStatus.Active)
            .Sum(x => x.ShareCount);
        var regularPayoutShares = _payouts
            .Where(x => x.RecipientType == PayoutRecipientType.Membership)
            .Where(x => _memberships.Any(m => m.Id == x.MembershipId && m.Kind == MembershipKind.Paying))
            .Sum(x => x.ShareCount);
        Guard.Against(payingShares != regularPayoutShares, "association.financial_imbalance",
            "Paying member obligations must equal regular scheduled payout value; reserve and advance entries are excluded.");
    }
}
