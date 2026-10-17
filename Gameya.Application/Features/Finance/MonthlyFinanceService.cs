using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Finance;

public sealed class MonthlyFinanceService(IApplicationDbContext db, IClock clock) : IMonthlyFinanceService
{
    public async Task<MonthlyFinanceDto> GetAsync(string userId, bool includeAllOwners, int year, int month,
        CancellationToken cancellationToken)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
            throw new RequestValidationException(new Dictionary<string, string[]>
                { ["month"] = ["يجب اختيار شهر وسنة صالحين."] });

        var associations = await db.Associations.AsNoTracking()
            .Where(x => (includeAllOwners || x.OwnerId == userId) && x.Status != AssociationStatus.Draft)
            .ToListAsync(cancellationToken);
        var associationById = associations.ToDictionary(x => x.Id);
        var associationIds = associations.Select(x => x.Id).ToArray();
        var selectedPeriods = await db.Periods.AsNoTracking()
            .Where(x => associationIds.Contains(x.AssociationId) && x.PeriodDate.Year == year && x.PeriodDate.Month == month)
            .ToListAsync(cancellationToken);

        // A cancelled association keeps periods up to its cancellation date but produces no future obligations.
        selectedPeriods = selectedPeriods.Where(period =>
        {
            var association = associationById[period.AssociationId];
            return association.Status != AssociationStatus.Cancelled || !association.UpdatedAtUtc.HasValue ||
                period.PeriodDate <= clock.EgyptDate(association.UpdatedAtUtc.Value);
        }).ToList();
        var periodIds = selectedPeriods.Select(x => x.Id).ToArray();
        var selectedAssociationIds = selectedPeriods.Select(x => x.AssociationId).Distinct().ToArray();

        var memberships = await db.Memberships.AsNoTracking()
            .Where(x => selectedAssociationIds.Contains(x.AssociationId) && x.Kind == MembershipKind.Paying)
            .ToListAsync(cancellationToken);
        var peopleIds = memberships.Select(x => x.PersonId).Distinct().ToArray();
        var names = await db.People.AsNoTracking().Where(x => peopleIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);

        var allocationRows = await db.PaymentAllocations.AsNoTracking()
            .Where(x => periodIds.Contains(x.PeriodId))
            .Join(db.Payments.AsNoTracking(), allocation => allocation.PaymentId, payment => payment.Id,
                (allocation, payment) => new { payment.PersonId, allocation.AssociationId, allocation.PeriodId, allocation.AllocatedAmount })
            .GroupBy(x => new { x.PersonId, x.AssociationId, x.PeriodId })
            .Select(x => new { x.Key.PersonId, x.Key.AssociationId, x.Key.PeriodId, Amount = x.Sum(y => y.AllocatedAmount) })
            .ToListAsync(cancellationToken);
        var creditRows = await db.AssociationCreditApplications.AsNoTracking()
            .Where(x => periodIds.Contains(x.PeriodId))
            .GroupBy(x => new { x.PersonId, x.AssociationId, x.PeriodId })
            .Select(x => new { x.Key.PersonId, x.Key.AssociationId, x.Key.PeriodId, Amount = x.Sum(y => y.Amount) })
            .ToListAsync(cancellationToken);
        var allocatedByObligation = allocationRows.ToDictionary(
            x => (x.PersonId, x.AssociationId, x.PeriodId), x => x.Amount);
        var creditsByObligation = creditRows.ToDictionary(
            x => (x.PersonId, x.AssociationId, x.PeriodId), x => x.Amount);

        var obligationRows = new List<ObligationRow>();
        foreach (var membership in memberships)
        {
            var association = associationById[membership.AssociationId];
            var joined = clock.EgyptDate(membership.JoinedAtUtc);
            var left = membership.LeftAtUtc.HasValue
                ? clock.EgyptDate(membership.LeftAtUtc.Value)
                : (DateOnly?)null;
            foreach (var period in selectedPeriods.Where(x => x.AssociationId == association.Id &&
                         x.PeriodDate >= joined && (!left.HasValue || x.PeriodDate <= left.Value)))
            {
                var required = FinancialCalculationRules.ObligationAmount(
                    association.MonthlyContribution, membership.ShareCount);
                var key = (membership.PersonId, association.Id, period.Id);
                var allocated = allocatedByObligation.GetValueOrDefault(key);
                var credits = creditsByObligation.GetValueOrDefault(key);
                obligationRows.Add(new(membership.PersonId, association.Id, association.Name, period.Id,
                    period.PeriodNumber, period.DueDate, required, allocated + credits));
            }
        }

        // Multiple membership records may legitimately represent replacement shares; consolidate by person and period.
        var consolidated = obligationRows.GroupBy(x => new { x.PersonId, x.AssociationId, x.AssociationName,
                x.PeriodId, x.PeriodNumber, x.DueDate })
            .Select(group =>
            {
                var required = group.Sum(x => x.Required);
                // Allocation rows were joined per membership above, so read their common value only once.
                var paid = Math.Clamp(group.Select(x => x.RawPaid).DefaultIfEmpty().Max(), 0, required);
                var remaining = Math.Max(0, required - paid);
                return new ObligationResult(group.Key.PersonId, group.Key.AssociationId, group.Key.AssociationName,
                    group.Key.PeriodId, group.Key.PeriodNumber, group.Key.DueDate, required, paid, remaining,
                    group.Key.DueDate < clock.Today ? remaining : 0);
            }).ToList();

        var payouts = await db.Payouts.AsNoTracking().Where(x => periodIds.Contains(x.PeriodId))
            .ToListAsync(cancellationToken);
        var membershipPerson = memberships.ToDictionary(x => x.Id, x => x.PersonId);
        var periodById = selectedPeriods.ToDictionary(x => x.Id);
        var payoutRows = payouts.Select(payout =>
        {
            Guid? personId = payout.RecipientType == PayoutRecipientType.Membership && payout.MembershipId.HasValue
                ? membershipPerson.GetValueOrDefault(payout.MembershipId.Value)
                : payout.BeneficiaryPersonId;
            var association = associationById[payout.AssociationId];
            var period = periodById[payout.PeriodId];
            return personId.HasValue
                ? new PayoutRow(personId.Value, association.Id, association.Name, period.Id, period.PeriodNumber,
                    payout.ShareCount * association.FullSharePayoutValue)
                : null;
        }).Where(x => x is not null).Cast<PayoutRow>().ToList();

        var payoutPersonIds = payoutRows.Select(x => x.PersonId).Distinct().Except(names.Keys).ToArray();
        if (payoutPersonIds.Length != 0)
        {
            var payoutNames = await db.People.AsNoTracking().Where(x => payoutPersonIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
            foreach (var item in payoutNames) names[item.Key] = item.Value;
        }

        var collections = consolidated.GroupBy(x => x.PersonId).Select(group =>
        {
            var personPayouts = payoutRows.Where(x => x.PersonId == group.Key).ToList();
            var required = group.Sum(x => x.Required);
            var paid = group.Sum(x => x.Paid);
            var scheduled = personPayouts.Sum(x => x.Amount);
            return new MonthlyCollectionPersonDto(group.Key, names.GetValueOrDefault(group.Key, "—"), required,
                paid, group.Sum(x => x.Remaining), group.Sum(x => x.Overdue), scheduled,
                Math.Max(0, scheduled - required), Math.Max(0, required - scheduled),
                group.Select(ToDto).OrderBy(x => x.AssociationName).ToArray(),
                personPayouts.Select(ToDto).OrderBy(x => x.AssociationName).ToArray());
        }).OrderBy(x => x.PersonName).ToArray();

        var payoutRecipients = payoutRows.GroupBy(x => x.PersonId).Select(group =>
        {
            var contributions = consolidated.Where(x => x.PersonId == group.Key).ToList();
            var scheduled = group.Sum(x => x.Amount);
            var required = contributions.Sum(x => x.Required);
            return new MonthlyPayoutPersonDto(group.Key, names.GetValueOrDefault(group.Key, "—"), scheduled,
                required, contributions.Sum(x => x.Overdue), Math.Max(0, scheduled - required),
                Math.Max(0, required - scheduled), group.Select(ToDto).OrderBy(x => x.AssociationName).ToArray(),
                contributions.Select(ToDto).OrderBy(x => x.AssociationName).ToArray());
        }).OrderBy(x => x.PersonName).ToArray();

        var totalRequired = collections.Sum(x => x.Required);
        var totalPaid = collections.Sum(x => x.Paid);
        return new MonthlyFinanceDto(year, month, FinancialCalculationRules.MonthLabel(year, month), totalRequired, totalPaid,
            collections.Sum(x => x.Remaining), collections.Sum(x => x.Overdue),
            collections.Count(x => x.Overdue > 0), totalRequired == 0 ? 0 : Math.Min(100, totalPaid / totalRequired * 100),
            associations.Count(x => x.Status == AssociationStatus.Active), collections.Length, payoutRecipients.Length,
            collections, payoutRecipients);
    }

    private static MonthlyAssociationAmountDto ToDto(ObligationResult x) => new(x.AssociationId,
        x.AssociationName, x.PeriodId, x.PeriodNumber, x.DueDate, x.Required, x.Paid, x.Remaining, x.Overdue);
    private static MonthlyPayoutAmountDto ToDto(PayoutRow x) => new(x.AssociationId, x.AssociationName,
        x.PeriodId, x.PeriodNumber, x.Amount);

    private sealed record ObligationRow(Guid PersonId, Guid AssociationId, string AssociationName, Guid PeriodId,
        int PeriodNumber, DateOnly DueDate, decimal Required, decimal RawPaid);
    private sealed record ObligationResult(Guid PersonId, Guid AssociationId, string AssociationName, Guid PeriodId,
        int PeriodNumber, DateOnly DueDate, decimal Required, decimal Paid, decimal Remaining, decimal Overdue);
    private sealed record PayoutRow(Guid PersonId, Guid AssociationId, string AssociationName, Guid PeriodId,
        int PeriodNumber, decimal Amount);
}
