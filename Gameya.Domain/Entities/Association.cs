using Gameya.Domain.Common;
using Gameya.Domain.Enums;
using Gameya.Domain.Events;
using Gameya.Domain.Exceptions;
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
        Name = NormalizeDisplayName(name);
        NormalizedName = NormalizeName(name);
        StartDate = startDate;
        NumberOfMonths = numberOfMonths;
        MonthlyContribution = monthlyContribution;
        OwnerId = ownerId;
        Status = AssociationStatus.Draft;
        CreatedAtUtc = now;
    }

    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
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

    public AssociationMembership AddMember(Guid personId, decimal shareCount, MembershipKind kind, DateTimeOffset now,
        int displayOrder = -1)
    {
        Guard.Against(Status != AssociationStatus.Draft, "association.members_after_start", "Normal members can only be added while the association is in draft.");
        Guard.Against(_memberships.Any(x => x.PersonId == personId && x.Status == MembershipStatus.Active),
            "membership.duplicate", "This person is already an active member.");
        var membership = new AssociationMembership(Id, personId, shareCount, kind, now,
            displayOrder < 0 ? _memberships.Count : displayOrder);
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

    public void ResetDraftConfiguration(string name, DateOnly startDate, int numberOfMonths,
        decimal monthlyContribution, IEnumerable<(DateOnly PeriodDate, DateOnly DueDate)> dates,
        DateTimeOffset now)
    {
        Guard.Against(Status != AssociationStatus.Draft, "association.draft_required",
            "The complete association configuration can only be replaced while it is a draft.");
        Guard.Against(string.IsNullOrWhiteSpace(name), "association.name_required", "Association name is required.");
        Guard.Against(numberOfMonths <= 0, "association.invalid_months", "Number of months must be positive.");
        Guard.Against(monthlyContribution <= 0, "association.invalid_contribution", "Monthly contribution must be positive.");
        _payouts.Clear();
        _memberships.Clear();
        _periods.Clear();
        Name = NormalizeDisplayName(name);
        NormalizedName = NormalizeName(name);
        StartDate = startDate;
        NumberOfMonths = numberOfMonths;
        MonthlyContribution = monthlyContribution;
        SetPeriods(dates);
        UpdatedAtUtc = now;
    }

    public PayoutAssignment AddPayout(Guid periodId, PayoutRecipientType type, Guid? membershipId,
        decimal shareCount, DateTimeOffset now, Guid? beneficiaryPersonId = null)
        => AddPayout(periodId, type, membershipId, shareCount, DateOnly.FromDateTime(now.UtcDateTime), now,
            beneficiaryPersonId);

    public PayoutAssignment AddPayout(Guid periodId, PayoutRecipientType type, Guid? membershipId,
        decimal shareCount, DateOnly today, DateTimeOffset now, Guid? beneficiaryPersonId = null)
    {
        Guard.Against(Status is AssociationStatus.Completed or AssociationStatus.Cancelled,
            "association.closed", "A closed association cannot receive payout assignments.");
        var period = _periods.SingleOrDefault(x => x.Id == periodId);
        Guard.Against(period is null, "period.not_found", "Period was not found.");
        Guard.Against(Status is AssociationStatus.Active or AssociationStatus.Paused && period!.PeriodDate <= today,
            "payout.future_only", "Only future payout assignments can be added after the association starts.");
        if (membershipId is not null)
            Guard.Against(_memberships.All(x => x.Id != membershipId), "membership.not_found", "Membership was not found.");
        var payout = new PayoutAssignment(Id, periodId, type, membershipId, shareCount, now, beneficiaryPersonId);
        _payouts.Add(payout);
        return payout;
    }

    public void MoveFuturePayout(Guid payoutId, Guid periodId, DateOnly today, DateTimeOffset now)
    {
        var payout = _payouts.SingleOrDefault(x => x.Id == payoutId);
        Guard.Against(payout is null, "payout.not_found", "Payout was not found.");
        var sourcePeriod = _periods.Single(x => x.Id == payout!.PeriodId);
        var period = _periods.SingleOrDefault(x => x.Id == periodId);
        Guard.Against(period is null, "period.not_found", "Period was not found.");
        Guard.Against(sourcePeriod.PeriodDate <= today || period!.PeriodDate <= today,
            "payout.future_only", "Only future payout assignments can be changed.");
        payout!.Reschedule(periodId);
        UpdatedAtUtc = now;
    }

    public void RemoveFuturePayout(Guid payoutId, DateOnly today, DateTimeOffset now)
    {
        var payout = _payouts.SingleOrDefault(x => x.Id == payoutId);
        Guard.Against(payout is null, "payout.not_found", "Payout was not found.");
        var period = _periods.Single(x => x.Id == payout!.PeriodId);
        Guard.Against(payout!.Status == PayoutStatus.Completed || period.PeriodDate <= today,
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
        var errors = ValidateForStart();
        if (errors.Count != 0) throw new AssociationBusinessValidationException(errors);
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

    public void Resume(DateTimeOffset now)
    {
        Guard.Against(Status != AssociationStatus.Paused, "association.resume_from_paused", "Only a paused association can resume.");
        Status = AssociationStatus.Active;
        UpdatedAtUtc = now;
    }

    public void CompletePayout(Guid payoutId, DateOnly today, DateTimeOffset now)
    {
        Guard.Against(Status is not (AssociationStatus.Active or AssociationStatus.Paused),
            "payout.invalid_association_state", "Payouts can only complete after the association starts.");
        var payout = _payouts.SingleOrDefault(x => x.Id == payoutId);
        Guard.Against(payout is null, "payout.not_found", "Payout was not found.");
        var period = _periods.Single(x => x.Id == payout!.PeriodId);
        Guard.Against(period.PeriodDate > today, "payout.not_due", "A future payout cannot be completed.");
        payout!.Complete(now);
        if (_payouts.Count != 0 && _payouts.All(x => x.Status == PayoutStatus.Completed))
        {
            foreach (var item in _periods) item.Close();
            Status = AssociationStatus.Completed;
            Raise(new AssociationCompleted(Id, now));
        }
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

    public void Cancel(DateTimeOffset now)
    {
        Guard.Against(Status is AssociationStatus.Completed or AssociationStatus.Cancelled,
            "association.cancel_state", "A completed or cancelled association cannot be cancelled.");
        Status = AssociationStatus.Cancelled;
        UpdatedAtUtc = now;
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
            var membership = new AssociationMembership(Id, item.PersonId, item.ShareCount, MembershipKind.Paying, now,
                _memberships.Count);
            _memberships.Add(membership);
            links.Add(new ReplacementShare(membership.Id, item.ShareCount));
        }
        var replacement = new Replacement(Id, old.Id, links, reason, now);
        _replacements.Add(replacement);
        UpdatedAtUtc = now;
        return replacement;
    }

    public IReadOnlyCollection<AssociationValidationError> ValidateForStart()
    {
        var errors = new List<AssociationValidationError>();
        if (_periods.Count != NumberOfMonths)
            errors.Add(new("association.periods_required", "numberOfMonths", null,
                "All association periods must exist before starting."));
        var payingMembers = _memberships
            .Where(x => x.Kind == MembershipKind.Paying && x.Status == MembershipStatus.Active)
            .ToList();
        if (payingMembers.Count == 0)
            errors.Add(new("association.members_required", "members", null,
                "At least one paying member is required."));
        foreach (var period in _periods.OrderBy(x => x.PeriodNumber))
            if (_payouts.All(x => x.PeriodId != period.Id))
                errors.Add(new("association.period_payout_required", $"periods[{period.PeriodNumber - 1}]", period.PeriodNumber,
                    "Every period must have at least one payout assignment."));
        foreach (var member in payingMembers)
        {
            var scheduledShares = _payouts
                .Where(x => x.RecipientType == PayoutRecipientType.Membership && x.MembershipId == member.Id)
                .Sum(x => x.ShareCount);
            if (scheduledShares != member.ShareCount)
                errors.Add(new("association.member_payout_mismatch", $"members[{member.DisplayOrder}].shareCount", null,
                    "The member's scheduled payout shares must equal their subscribed shares."));
        }
        foreach (var payout in _payouts.Where(x => x.RecipientType is PayoutRecipientType.ReserveSlot or PayoutRecipientType.AdvanceSlot))
        {
            var period = _periods.SingleOrDefault(x => x.Id == payout.PeriodId);
            if (payout.ShareCount != 1)
                errors.Add(new("payout.virtual_full_share", "payouts", period?.PeriodNumber,
                    "Reserve and advance slots must represent exactly one full share."));
            if (payout.MembershipId is not null)
                errors.Add(new("payout.virtual_cannot_be_person", "payouts", period?.PeriodNumber,
                    "Reserve and advance slots cannot be stored as people."));
        }
        var obligationShares = payingMembers.Sum(x => x.ShareCount);
        var payingMembershipIds = payingMembers.Select(x => x.Id).ToHashSet();
        var scheduledMemberShares = _payouts.Where(x => x.RecipientType == PayoutRecipientType.Membership
                && x.MembershipId.HasValue && payingMembershipIds.Contains(x.MembershipId.Value))
            .Sum(x => x.ShareCount);
        if (obligationShares != scheduledMemberShares)
            errors.Add(new("association.financial_imbalance", "payouts", null,
                "Paying member obligations must equal their regular scheduled payout shares."));
        return errors;
    }

    public static string NormalizeName(string name) => NormalizeDisplayName(name).ToUpperInvariant();
    private static string NormalizeDisplayName(string name) =>
        string.Join(' ', name.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
