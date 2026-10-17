using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Payments;

public sealed record PaymentPlanningRequest(Guid PersonId, decimal Amount, int? AdvanceYear = null,
    int? AdvanceMonth = null, decimal? AdvanceAmount = null,
    IReadOnlyCollection<Guid>? AssociationIds = null, Guid? CreditAssociationId = null);

public sealed record PlannedPaymentAllocation(Guid AssociationId, Guid PeriodId, DateOnly DueDate,
    decimal Amount);

public sealed record PlannedCreditApplication(Guid AssociationId, Guid PeriodId, decimal Amount);

public sealed record PaymentAllocationPlan(PaymentPreviewDto Preview,
    IReadOnlyCollection<PlannedPaymentAllocation> Allocations,
    IReadOnlyCollection<PlannedCreditApplication> CreditApplications,
    Guid CreditAssociationId);

public sealed class PaymentAllocationService(
    IApplicationDbContext db,
    ICurrentUser current,
    IAssociationAccessService access,
    IClock clock)
{
    public async Task<PaymentPageDto> GetPageAsync(Guid personId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(personId, null, cancellationToken);
        var currentStart = new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        var currentEnd = currentStart.AddMonths(1).AddDays(-1);
        var effective = ApplyCredits(state, DateOnly.MaxValue);
        var rows = effective.Rows;

        var currentRows = rows.Where(x => SameMonth(x.PeriodDate, currentStart)).ToArray();
        var arrearRows = rows.Where(x => x.PeriodDate < currentStart && x.Remaining > 0).ToArray();
        var nextGroup = rows.Where(x => x.PeriodDate > currentEnd && x.Remaining > 0)
            .GroupBy(x => (x.PeriodDate.Year, x.PeriodDate.Month))
            .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month).FirstOrDefault();

        var associationDtos = new List<AccessibleAssociationDueDto>();
        foreach (var association in state.Associations.Values.OrderBy(x => x.Name))
        {
            var associationRows = rows.Where(x => x.AssociationId == association.Id).ToArray();
            var associationCurrent = associationRows.Where(x => SameMonth(x.PeriodDate, currentStart)).ToArray();
            var associationArrears = associationRows.Where(x => x.PeriodDate < currentStart).Sum(x => x.Remaining);
            var currentRequired = associationCurrent.Sum(x => x.Required);
            var currentPaid = associationCurrent.Sum(x => x.Paid);
            var currentRemaining = associationCurrent.Sum(x => x.Remaining);
            var shares = state.Memberships.Where(x => x.AssociationId == association.Id &&
                    IsMembershipEligible(x, currentStart))
                .Sum(x => x.ShareCount);
            if (shares == 0)
                shares = state.Memberships.Where(x => x.AssociationId == association.Id && !x.LeftAt.HasValue)
                    .Sum(x => x.ShareCount);
            var requiredThroughCurrent = associationRows.Where(x => x.PeriodDate <= currentEnd).Sum(x => x.Required);
            var paidThroughCurrent = associationRows.Where(x => x.PeriodDate <= currentEnd).Sum(x => x.Paid);
            var oldest = associationRows.Where(x => x.PeriodDate <= currentEnd && x.Remaining > 0)
                .OrderBy(x => x.PeriodDate).ThenBy(x => x.DueDate).Select(x => (DateOnly?)x.DueDate)
                .FirstOrDefault();
            var isCancelled = association.Status == AssociationStatus.Cancelled;
            associationDtos.Add(new AccessibleAssociationDueDto(association.Id, association.Name,
                association.Status, shares,
                isCancelled ? 0m : (associationCurrent.Length == 0
                    ? FinancialCalculationRules.ObligationAmount(association.MonthlyContribution, shares)
                    : currentRequired),
                associationCurrent.OrderBy(x => x.PeriodNumber).Select(x => (int?)x.PeriodNumber).FirstOrDefault(),
                currentRequired, currentPaid, currentRemaining, associationArrears,
                associationArrears + currentRemaining, associationRows.Sum(x => x.Remaining), oldest,
                effective.RemainingCredits.GetValueOrDefault(association.Id),
                FinancialCalculationRules.PaymentStatus(requiredThroughCurrent, paidThroughCurrent)));
        }

        var arrearMonths = arrearRows.GroupBy(x => (x.PeriodDate.Year, x.PeriodDate.Month))
            .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month)
            .Select(x => ToMonthDto(x.Key.Year, x.Key.Month, x)).ToArray();
        var currentMonth = ToMonthDto(currentStart.Year, currentStart.Month, currentRows);
        var nextMonth = nextGroup is null ? null : ToMonthDto(nextGroup.Key.Year, nextGroup.Key.Month, nextGroup);
        var dueItems = arrearRows.Concat(currentRows.Where(x => x.Remaining > 0))
            .OrderBy(x => x.PeriodDate).ThenBy(x => x.AssociationId).Select(ToDueItem).ToArray();

        return new PaymentPageDto(state.PersonId, state.FullName, state.SubscriberCode, associationDtos,
            dueItems, currentStart.Year, currentStart.Month,
            FinancialCalculationRules.MonthLabel(currentStart.Year, currentStart.Month), arrearMonths,
            currentMonth, nextMonth, arrearRows.Sum(x => x.Remaining), currentRows.Sum(x => x.Remaining),
            dueItems.Sum(x => x.Amount), rows.Sum(x => x.Remaining));
    }

    public async Task<PaymentAllocationPlan> BuildPlanAsync(PaymentPlanningRequest request,
        CancellationToken cancellationToken)
    {
        var state = await LoadAsync(request.PersonId, request.AssociationIds, cancellationToken);
        var currentStart = new DateOnly(clock.Today.Year, clock.Today.Month, 1);
        var currentEnd = currentStart.AddMonths(1).AddDays(-1);
        var allEffective = ApplyCredits(state, DateOnly.MaxValue);
        var nextMonth = allEffective.Rows.Where(x => x.PeriodDate > currentEnd && x.Remaining > 0)
            .Select(x => (x.PeriodDate.Year, x.PeriodDate.Month)).Distinct()
            .OrderBy(x => x.Year).ThenBy(x => x.Month).FirstOrDefault();
        var hasAdvance = request.AdvanceYear.HasValue || request.AdvanceMonth.HasValue || (request.AdvanceAmount.HasValue && request.AdvanceAmount.Value > 0);
        if (hasAdvance && (!request.AdvanceYear.HasValue || !request.AdvanceMonth.HasValue ||
                           request.AdvanceMonth is < 1 or > 12 || (request.AdvanceAmount.HasValue && request.AdvanceAmount.Value <= 0)))
            throw new RequestValidationException(new Dictionary<string, string[]>
                { ["advanceMonth"] = ["اختر شهر الدفع المقدم وأدخل مبلغًا صحيحًا."] });
        if (hasAdvance && (nextMonth == default || nextMonth.Year != request.AdvanceYear ||
                           nextMonth.Month != request.AdvanceMonth))
            throw new ConflictException("payment.advance_month_not_next",
                "يمكن الدفع مقدمًا لأقرب شهر مستحق فقط، ولا يمكن تجاوز استحقاق أقدم.");

        var boundary = hasAdvance
            ? new DateOnly(request.AdvanceYear!.Value, request.AdvanceMonth!.Value, 1).AddMonths(1).AddDays(-1)
            : currentEnd;
        var effective = ApplyCredits(state, boundary);
        var targetRows = effective.Rows.Where(x => x.PeriodDate <= currentEnd ||
                hasAdvance && x.PeriodDate.Year == request.AdvanceYear && x.PeriodDate.Month == request.AdvanceMonth)
            .OrderBy(x => x.PeriodDate).ThenBy(x => x.DueDate).ThenBy(x => x.AssociationId)
            .ThenBy(x => x.PeriodId).ToArray();
        var remainingPayment = request.Amount;
        var actual = new List<PlannedPaymentAllocation>();
        var previewItems = new List<PaymentPreviewAllocationDto>();
        decimal futureAllocated = 0;

        foreach (var monthGroup in targetRows.GroupBy(x => (x.PeriodDate.Year, x.PeriodDate.Month))
                     .OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month))
        {
            var groupRows = monthGroup.Where(x => x.Remaining > 0).ToArray();
            var outstanding = groupRows.Sum(x => x.Remaining);
            var groupLimit = outstanding;
            var isAdvance = monthGroup.Key.Year == request.AdvanceYear && monthGroup.Key.Month == request.AdvanceMonth;
            if (isAdvance && request.AdvanceAmount.HasValue)
                groupLimit = Math.Min(groupLimit, request.AdvanceAmount.Value);
            var amountForMonth = Math.Min(remainingPayment, groupLimit);
            foreach (var allocation in AllocateProportionally(groupRows, amountForMonth))
            {
                allocation.Row.Paid += allocation.Amount;
                actual.Add(new PlannedPaymentAllocation(allocation.Row.AssociationId, allocation.Row.PeriodId,
                    allocation.Row.DueDate, allocation.Amount));
                if (isAdvance) futureAllocated += allocation.Amount;
            }
            remainingPayment -= amountForMonth;
        }

        foreach (var row in targetRows)
        {
            var allocated = actual.Where(x => x.AssociationId == row.AssociationId && x.PeriodId == row.PeriodId)
                .Sum(x => x.Amount);
            if (allocated <= 0 && row.Remaining <= 0)
                continue;

            previewItems.Add(new PaymentPreviewAllocationDto(row.AssociationId, row.AssociationName,
                row.PeriodId, row.PeriodNumber, row.PeriodDate, row.DueDate,
                row.PeriodDate < currentStart ? "Arrears" : row.PeriodDate <= currentEnd ? "Current" : "Advance",
                row.Required, row.Paid - allocated, allocated, row.Remaining,
                FinancialCalculationRules.PaymentStatus(row.Required, row.Paid)));
        }

        var creditAssociationId = ResolveCreditAssociation(state, targetRows, actual, request);
        var credits = remainingPayment > 0
            ? new[] { new PaymentPreviewCreditDto(creditAssociationId,
                state.Associations[creditAssociationId].Name, remainingPayment) }
            : [];
        var advanceTargetDues = hasAdvance
            ? targetRows.Where(x => x.PeriodDate.Year == request.AdvanceYear && x.PeriodDate.Month == request.AdvanceMonth)
                .Sum(x => x.Remaining)
            : 0;
        var intendedAdvance = hasAdvance
            ? Math.Min(request.AdvanceAmount ?? advanceTargetDues, request.Amount)
            : 0;
        var wasReallocated = hasAdvance && futureAllocated < intendedAdvance;
        var notice = wasReallocated
            ? "تم توجيه جزء من مبلغ الدفع المقدم إلى الاستحقاقات الأقدم أولًا وفق قاعدة الأقدم فالأقدم."
            : remainingPayment > 0
                ? "المبلغ الزائد لا يقابل استحقاقًا مسموحًا حاليًا، وسيُحفظ كرصيد للجمعية الموضحة."
                : null;
        var allocatedAmount = actual.Sum(x => x.Amount);
        var token = CreateToken(request, actual, credits, effective.CreditApplications);
        var preview = new PaymentPreviewDto(request.PersonId, request.Amount, allocatedAmount, remainingPayment,
            wasReallocated, notice, token, previewItems, credits);
        return new PaymentAllocationPlan(preview, actual, effective.CreditApplications, creditAssociationId);
    }

    private async Task<PaymentState> LoadAsync(Guid personId, IReadOnlyCollection<Guid>? filterAssociationIds, CancellationToken cancellationToken)
    {
        var userId = current.UserId ?? throw new ForbiddenException();
        var accessible = access.GetAccessibleAssociationIds(userId, current.IsInRole(AppRoles.Admin),
            current.IsInRole(AppRoles.Owner));
        if (filterAssociationIds != null && filterAssociationIds.Count > 0)
        {
            accessible = accessible.Where(id => filterAssociationIds.Contains(id));
        }
        var memberships = await db.Memberships.AsNoTracking()
            .Where(x => x.PersonId == personId && x.Kind == MembershipKind.Paying && accessible.Contains(x.AssociationId))
            .Join(db.Associations.AsNoTracking().Where(x => x.Status != AssociationStatus.Draft),
                membership => membership.AssociationId, association => association.Id,
                (membership, association) => new
                {
                    membership.AssociationId, membership.ShareCount, membership.JoinedAtUtc,
                    membership.LeftAtUtc, Association = association
                }).ToListAsync(cancellationToken);
        if (memberships.Count == 0)
            throw new NotFoundException("person.not_found", "Person was not found.");
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId && x.IsActive,
            cancellationToken) ?? throw new NotFoundException("person.not_found", "Person was not found.");
        var associations = memberships.Select(x => x.Association).DistinctBy(x => x.Id)
            .ToDictionary(x => x.Id, x => new AssociationInfo(x.Id, x.Name, x.MonthlyContribution, x.Status,
                x.UpdatedAtUtc.HasValue ? clock.EgyptDate(x.UpdatedAtUtc.Value) : null));
        var membershipRows = memberships.Select(x => new MembershipInfo(x.AssociationId, x.ShareCount,
            clock.EgyptDate(x.JoinedAtUtc), x.LeftAtUtc.HasValue ? clock.EgyptDate(x.LeftAtUtc.Value) : null)).ToArray();
        var associationIds = associations.Keys.ToArray();
        var periods = await db.Periods.AsNoTracking().Where(x => associationIds.Contains(x.AssociationId))
            .ToListAsync(cancellationToken);
        var allocations = await db.PaymentAllocations.AsNoTracking()
            .Where(x => associationIds.Contains(x.AssociationId))
            .Join(db.Payments.AsNoTracking().Where(x => x.PersonId == personId), allocation => allocation.PaymentId,
                payment => payment.Id, (allocation, _) => allocation)
            .GroupBy(x => new { x.AssociationId, x.PeriodId })
            .Select(x => new { x.Key.AssociationId, x.Key.PeriodId, Amount = x.Sum(y => y.AllocatedAmount) })
            .ToListAsync(cancellationToken);
        var applications = await db.AssociationCreditApplications.AsNoTracking()
            .Where(x => x.PersonId == personId && associationIds.Contains(x.AssociationId))
            .GroupBy(x => new { x.AssociationId, x.PeriodId })
            .Select(x => new { x.Key.AssociationId, x.Key.PeriodId, Amount = x.Sum(y => y.Amount) })
            .ToListAsync(cancellationToken);
        var credits = await db.AssociationCredits.AsNoTracking()
            .Where(x => x.PersonId == personId && associationIds.Contains(x.AssociationId))
            .GroupBy(x => x.AssociationId).Select(x => new { x.Key, Amount = x.Sum(y => y.Amount) })
            .ToDictionaryAsync(x => x.Key, x => x.Amount, cancellationToken);

        var required = new Dictionary<(Guid AssociationId, Guid PeriodId), ObligationRow>();
        foreach (var membership in membershipRows)
        {
            var association = associations[membership.AssociationId];
            foreach (var period in periods.Where(x => x.AssociationId == association.Id &&
                         IsMembershipEligible(membership, x.PeriodDate) &&
                         IsAssociationPeriodEligible(association, x.PeriodDate)))
            {
                var key = (association.Id, period.Id);
                var amount = FinancialCalculationRules.ObligationAmount(association.MonthlyContribution,
                    membership.ShareCount);
                if (required.TryGetValue(key, out var existing)) existing.Required += amount;
                else required[key] = new ObligationRow(association.Id, association.Name, period.Id,
                    period.PeriodNumber, period.PeriodDate, period.DueDate, amount, 0);
            }
        }
        foreach (var row in required.Values)
        {
            var paid = allocations.Where(x => x.AssociationId == row.AssociationId && x.PeriodId == row.PeriodId)
                           .Sum(x => x.Amount)
                       + applications.Where(x => x.AssociationId == row.AssociationId && x.PeriodId == row.PeriodId)
                           .Sum(x => x.Amount);
            row.Paid = Math.Clamp(paid, 0, row.Required);
        }
        var appliedByAssociation = applications.GroupBy(x => x.AssociationId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var availableCredits = associations.Keys.ToDictionary(x => x,
            x => Math.Max(0, credits.GetValueOrDefault(x) - appliedByAssociation.GetValueOrDefault(x)));
        return new PaymentState(person.Id, person.FullName, person.SubscriberCode.Value, associations,
            membershipRows, required.Values.ToArray(), availableCredits);
    }

    private static CreditResult ApplyCredits(PaymentState state, DateOnly throughDate)
    {
        var rows = state.Rows.Select(x => x.Clone()).ToArray();
        var credits = state.AvailableCredits.ToDictionary(x => x.Key, x => x.Value);
        var applications = new List<PlannedCreditApplication>();
        foreach (var associationId in state.Associations.Keys.Order())
        {
            var available = credits.GetValueOrDefault(associationId);
            foreach (var row in rows.Where(x => x.AssociationId == associationId && x.PeriodDate <= throughDate)
                         .OrderBy(x => x.PeriodDate).ThenBy(x => x.DueDate).ThenBy(x => x.PeriodId))
            {
                var applied = Math.Min(row.Remaining, available);
                if (applied <= 0) continue;
                row.Paid += applied;
                available -= applied;
                applications.Add(new PlannedCreditApplication(row.AssociationId, row.PeriodId, applied));
                if (available == 0) break;
            }
            credits[associationId] = available;
        }
        return new CreditResult(rows, credits, applications);
    }

    internal static IReadOnlyCollection<ProportionalAllocation> AllocateProportionally(
        IReadOnlyCollection<ObligationRow> source, decimal amount)
    {
        if (amount <= 0 || source.Count == 0) return [];
        var rows = source.Where(x => x.Remaining > 0).OrderBy(x => x.AssociationId).ThenBy(x => x.PeriodId).ToArray();
        var target = Math.Min(FinancialCalculationRules.Currency(amount), rows.Sum(x => x.Remaining));
        var total = rows.Sum(x => x.Remaining);
        var values = rows.Select(row =>
        {
            var raw = target * row.Remaining / total;
            var floor = decimal.Floor(raw * 100m) / 100m;
            return new AllocationCandidate(row, Math.Min(floor, row.Remaining), raw - floor);
        }).ToArray();
        var cents = (int)decimal.Round((target - values.Sum(x => x.Amount)) * 100m, 0,
            MidpointRounding.AwayFromZero);
        foreach (var candidate in values.OrderByDescending(x => x.Fraction).ThenBy(x => x.Row.AssociationId)
                     .ThenBy(x => x.Row.PeriodId))
        {
            if (cents == 0) break;
            if (candidate.Amount + .01m > candidate.Row.Remaining) continue;
            candidate.Amount += .01m;
            cents--;
        }
        if (cents != 0)
        {
            foreach (var candidate in values.OrderBy(x => x.Row.AssociationId).ThenBy(x => x.Row.PeriodId))
            {
                while (cents > 0 && candidate.Amount + .01m <= candidate.Row.Remaining)
                {
                    candidate.Amount += .01m;
                    cents--;
                }
                if (cents == 0) break;
            }
        }
        return values.Where(x => x.Amount > 0).Select(x => new ProportionalAllocation(x.Row, x.Amount)).ToArray();
    }

    private static PaymentMonthDueDto ToMonthDto(int year, int month, IEnumerable<ObligationRow> source)
    {
        var rows = source.OrderBy(x => x.AssociationName).ThenBy(x => x.PeriodNumber).ToArray();
        var required = rows.Sum(x => x.Required);
        var paid = rows.Sum(x => x.Paid);
        return new PaymentMonthDueDto(year, month, FinancialCalculationRules.MonthLabel(year, month), required,
            paid, rows.Sum(x => x.Remaining), FinancialCalculationRules.PaymentStatus(required, paid),
            rows.Select(ToDueItem).ToArray());
    }

    private static PaymentDueItemDto ToDueItem(ObligationRow row) => new(row.AssociationId,
        row.AssociationName, row.PeriodId, row.PeriodNumber, row.PeriodDate, row.DueDate, row.Required,
        row.Paid, row.Remaining, FinancialCalculationRules.PaymentStatus(row.Required, row.Paid));

    private static Guid ResolveCreditAssociation(PaymentState state, IReadOnlyCollection<ObligationRow> targetRows,
        IReadOnlyCollection<PlannedPaymentAllocation> allocations, PaymentPlanningRequest request)
    {
        if (request.CreditAssociationId.HasValue && state.Associations.ContainsKey(request.CreditAssociationId.Value))
            return request.CreditAssociationId.Value;

        if (request.AdvanceYear.HasValue)
        {
            var future = targetRows.Where(x => x.PeriodDate.Year == request.AdvanceYear &&
                    x.PeriodDate.Month == request.AdvanceMonth)
                .OrderBy(x => x.AssociationId).Select(x => (Guid?)x.AssociationId).FirstOrDefault();
            if (future.HasValue) return future.Value;
        }
        var last = allocations.LastOrDefault();
        return last?.AssociationId ?? state.Associations.Keys.Order().First();
    }

    private static string CreateToken(PaymentPlanningRequest request,
        IReadOnlyCollection<PlannedPaymentAllocation> allocations,
        IReadOnlyCollection<PaymentPreviewCreditDto> credits,
        IReadOnlyCollection<PlannedCreditApplication> creditApplications)
    {
        var canonical = new StringBuilder()
            .Append(request.PersonId).Append('|').Append(request.Amount.ToString("0.00", CultureInfo.InvariantCulture))
            .Append('|').Append(request.AdvanceYear).Append('|').Append(request.AdvanceMonth).Append('|')
            .Append(request.AdvanceAmount?.ToString("0.00", CultureInfo.InvariantCulture));
        foreach (var item in creditApplications.OrderBy(x => x.AssociationId).ThenBy(x => x.PeriodId))
            canonical.Append("|A:").Append(item.AssociationId).Append(':').Append(item.PeriodId).Append(':')
                .Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        foreach (var item in allocations.OrderBy(x => x.DueDate).ThenBy(x => x.AssociationId).ThenBy(x => x.PeriodId))
            canonical.Append("|P:").Append(item.AssociationId).Append(':').Append(item.PeriodId).Append(':')
                .Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        foreach (var item in credits.OrderBy(x => x.AssociationId))
            canonical.Append("|C:").Append(item.AssociationId).Append(':')
                .Append(item.Amount.ToString("0.00", CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private bool IsAssociationPeriodEligible(AssociationInfo association, DateOnly periodDate)
    {
        if (association.Status == AssociationStatus.Cancelled) return false;
        if (association.Status == AssociationStatus.Completed) return periodDate <= clock.Today;
        return true;
    }

    private static bool IsMembershipEligible(MembershipInfo membership, DateOnly periodDate) =>
        periodDate >= membership.JoinedAt && (!membership.LeftAt.HasValue || periodDate <= membership.LeftAt.Value);

    private static bool SameMonth(DateOnly value, DateOnly month) =>
        value.Year == month.Year && value.Month == month.Month;

    private sealed record AssociationInfo(Guid Id, string Name, decimal MonthlyContribution,
        AssociationStatus Status, DateOnly? ClosedAt);
    private sealed record MembershipInfo(Guid AssociationId, decimal ShareCount, DateOnly JoinedAt,
        DateOnly? LeftAt);
    private sealed record PaymentState(Guid PersonId, string FullName, string SubscriberCode,
        IReadOnlyDictionary<Guid, AssociationInfo> Associations, IReadOnlyCollection<MembershipInfo> Memberships,
        IReadOnlyCollection<ObligationRow> Rows, IReadOnlyDictionary<Guid, decimal> AvailableCredits);
    private sealed record CreditResult(IReadOnlyCollection<ObligationRow> Rows,
        IReadOnlyDictionary<Guid, decimal> RemainingCredits,
        IReadOnlyCollection<PlannedCreditApplication> CreditApplications);
    internal sealed class ObligationRow(Guid associationId, string associationName, Guid periodId,
        int periodNumber, DateOnly periodDate, DateOnly dueDate, decimal required, decimal paid)
    {
        public Guid AssociationId { get; } = associationId;
        public string AssociationName { get; } = associationName;
        public Guid PeriodId { get; } = periodId;
        public int PeriodNumber { get; } = periodNumber;
        public DateOnly PeriodDate { get; } = periodDate;
        public DateOnly DueDate { get; } = dueDate;
        public decimal Required { get; set; } = required;
        public decimal Paid { get; set; } = paid;
        public decimal Remaining => Math.Max(0, Required - Paid);
        public ObligationRow Clone() => new(AssociationId, AssociationName, PeriodId, PeriodNumber, PeriodDate,
            DueDate, Required, Paid);
    }
    internal sealed record ProportionalAllocation(ObligationRow Row, decimal Amount);
    private sealed class AllocationCandidate(ObligationRow row, decimal amount, decimal fraction)
    {
        public ObligationRow Row { get; } = row;
        public decimal Amount { get; set; } = amount;
        public decimal Fraction { get; } = fraction;
    }
}
