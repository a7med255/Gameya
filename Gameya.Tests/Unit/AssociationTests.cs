using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Domain.Exceptions;

namespace Gameya.Tests.Unit;

public sealed class AssociationTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Full_share_value_is_monthly_contribution_times_month_count()
    {
        var association = NewAssociation(20, 5_000m);
        Assert.Equal(100_000m, association.FullSharePayoutValue);
    }

    [Fact]
    public void Reserve_slot_is_exactly_one_share_and_has_no_person()
    {
        var association = NewAssociation();
        var period = association.Periods.First();
        var error = Assert.Throws<DomainException>(() =>
            association.AddPayout(period.Id, PayoutRecipientType.ReserveSlot, null, 0.5m, Now));
        Assert.Equal("payout.virtual_full_share", error.Code);
        var reserve = association.AddPayout(period.Id, PayoutRecipientType.ReserveSlot, null, 1m, Now);
        Assert.Null(reserve.MembershipId);
    }

    [Fact]
    public void A_period_can_have_multiple_payout_recipients()
    {
        var association = NewAssociation();
        var first = association.AddMember(Guid.NewGuid(), 0.5m, MembershipKind.Paying, Now);
        var second = association.AddMember(Guid.NewGuid(), 0.5m, MembershipKind.Paying, Now);
        var period = association.Periods.First();
        association.AddPayout(period.Id, PayoutRecipientType.Membership, first.Id, 0.5m, Now);
        association.AddPayout(period.Id, PayoutRecipientType.Membership, second.Id, 0.5m, Now);
        association.AddPayout(association.Periods.Last().Id, PayoutRecipientType.ReserveSlot, null, 1m, Now);
        Assert.Equal(2, association.Payouts.Count(x => x.PeriodId == period.Id));
        association.Start(Now);
    }

    [Fact]
    public void Start_excludes_advance_and_reserve_entries_from_balance_validation()
    {
        var association = NewAssociation();
        var paying = association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);
        association.AddPayout(association.Periods.First().Id, PayoutRecipientType.Membership, paying.Id, 1m, Now);
        association.AddPayout(association.Periods.Last().Id, PayoutRecipientType.AdvanceSlot, null, 1m, Now);
        association.Start(Now);
        Assert.Equal(AssociationStatus.Active, association.Status);
    }

    [Fact]
    public void Start_rejects_unbalanced_paying_shares()
    {
        var association = NewAssociation();
        association.AddMember(Guid.NewGuid(), 1.5m, MembershipKind.Paying, Now);
        var error = Assert.Throws<AssociationBusinessValidationException>(() => association.Start(Now));
        Assert.Contains(error.Errors, item => item.Code == "association.financial_imbalance");
    }

    [Fact]
    public void Start_requires_each_paying_members_payout_positions_to_match_their_shares()
    {
        var association = NewAssociation();
        var first = association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);
        association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);
        association.AddPayout(association.Periods.First().Id, PayoutRecipientType.Membership, first.Id, 2m, Now);

        var error = Assert.Throws<AssociationBusinessValidationException>(() => association.Start(Now));

        Assert.Equal(2, error.Errors.Count(item => item.Code == "association.member_payout_mismatch"));
    }

    [Fact]
    public void Normal_members_cannot_join_after_start()
    {
        var association = StartedAssociation(2m);
        var error = Assert.Throws<DomainException>(() => association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now.AddDays(1)));
        Assert.Equal("association.members_after_start", error.Code);
    }

    [Fact]
    public void Replacement_can_split_shares_without_overwriting_old_membership()
    {
        var association = StartedAssociation(2m);
        var old = association.Memberships.Single();
        var replacement = association.ReplaceMember(old.Id,
            [(Guid.NewGuid(), 1m), (Guid.NewGuid(), 0.5m), (Guid.NewGuid(), 0.5m)], "left", Now.AddDays(5));
        Assert.Equal(MembershipStatus.Replaced, old.Status);
        Assert.Equal(3, replacement.Shares.Count);
        Assert.Equal(4, association.Memberships.Count);
        Assert.Equal(2m, replacement.Shares.Sum(x => x.ShareCount));
    }

    [Fact]
    public void Replacement_must_preserve_total_shares()
    {
        var association = StartedAssociation(2m);
        var old = association.Memberships.Single();
        var error = Assert.Throws<DomainException>(() =>
            association.ReplaceMember(old.Id, [(Guid.NewGuid(), 1m)], null, Now.AddDays(2)));
        Assert.Equal("replacement.share_mismatch", error.Code);
    }

    [Fact]
    public void Completed_payout_cannot_be_rescheduled()
    {
        var association = NewAssociation();
        var member = association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);
        var payout = association.AddPayout(association.Periods.First().Id, PayoutRecipientType.Membership, member.Id, 1m, Now);
        payout.Complete(Now.AddDays(1));
        var error = Assert.Throws<DomainException>(() =>
            association.MoveFuturePayout(payout.Id, association.Periods.Last().Id, new DateOnly(2025, 12, 1), Now.AddDays(2)));
        Assert.Equal("payout.historical_immutable", error.Code);
    }

    [Fact]
    public void Historical_scheduled_payout_cannot_be_moved_into_a_future_period()
    {
        var association = NewAssociation();
        var member = association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);
        var payout = association.AddPayout(association.Periods.First().Id, PayoutRecipientType.Membership, member.Id, 1m, Now);

        var error = Assert.Throws<DomainException>(() => association.MoveFuturePayout(payout.Id,
            association.Periods.Last().Id, association.Periods.First().PeriodDate, Now.AddDays(1)));

        Assert.Equal("payout.future_only", error.Code);
    }

    [Fact]
    public void Paused_association_can_resume()
    {
        var association = StartedAssociation(1m);
        association.Pause(Now.AddDays(1));

        association.Resume(Now.AddDays(2));

        Assert.Equal(AssociationStatus.Active, association.Status);
    }

    [Fact]
    public void Completed_association_rejects_future_state_changes()
    {
        var association = StartedAssociation(1m);
        association.ClosePeriod(association.Periods.Single().Id, Now.AddMonths(1));
        association.Complete(Now.AddMonths(1));
        Assert.Equal(AssociationStatus.Completed, association.Status);
        Assert.Throws<DomainException>(() => association.AddPayout(association.Periods.Single().Id,
            PayoutRecipientType.ReserveSlot, null, 1m, Now.AddMonths(2)));
    }

    [Fact]
    public void Start_returns_all_missing_period_and_member_errors()
    {
        var association = NewAssociation(3);
        association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);

        var error = Assert.Throws<AssociationBusinessValidationException>(() => association.Start(Now));

        Assert.Equal(3, error.Errors.Count(item => item.Code == "association.period_payout_required"));
        Assert.Contains(error.Errors, item => item.Code == "association.member_payout_mismatch");
        Assert.Contains(error.Errors, item => item.Code == "association.financial_imbalance");
    }

    [Fact]
    public void Completing_the_last_outstanding_payout_completes_the_association()
    {
        var association = NewAssociation(1);
        var member = association.AddMember(Guid.NewGuid(), 1m, MembershipKind.Paying, Now);
        var payout = association.AddPayout(association.Periods.Single().Id,
            PayoutRecipientType.Membership, member.Id, 1m, Now);
        association.Start(Now);

        association.CompletePayout(payout.Id, association.Periods.Single().PeriodDate, Now.AddMonths(1));

        Assert.Equal(AssociationStatus.Completed, association.Status);
        Assert.All(association.Periods, period => Assert.Equal(PeriodStatus.Closed, period.Status));
    }

    [Fact]
    public void Cancellation_preserves_history_and_stops_the_association()
    {
        var association = StartedAssociation(1m);
        var payoutId = association.Payouts.Single().Id;

        association.Cancel(Now.AddDays(1));

        Assert.Equal(AssociationStatus.Cancelled, association.Status);
        Assert.Contains(association.Payouts, payout => payout.Id == payoutId);
    }

    [Theory]
    [InlineData("  Family   2027 ", "FAMILY 2027")]
    [InlineData("جمعية   العيلة", "جمعية العيلة")]
    public void Association_name_normalization_trims_and_collapses_whitespace(string value, string expected)
    {
        Assert.Equal(expected, Association.NormalizeName(value));
    }

    private static Association NewAssociation(int months = 2, decimal contribution = 5_000m)
    {
        var association = new Association("Test", new DateOnly(2026, 1, 1), months, contribution, "owner", Now);
        association.SetPeriods(Enumerable.Range(0, months).Select(i =>
            (new DateOnly(2026, 1, 1).AddMonths(i), new DateOnly(2026, 1, 10).AddMonths(i))));
        return association;
    }

    private static Association StartedAssociation(decimal shares)
    {
        var association = NewAssociation(1);
        var member = association.AddMember(Guid.NewGuid(), shares, MembershipKind.Paying, Now);
        association.AddPayout(association.Periods.Single().Id, PayoutRecipientType.Membership, member.Id, shares, Now);
        association.Start(Now);
        return association;
    }
}
