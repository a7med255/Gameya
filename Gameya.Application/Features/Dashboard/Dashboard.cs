using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Dashboard;

public sealed record GetDashboardQuery : IRequest<DashboardDto>;
public sealed class GetDashboardHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access, IClock clock)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var associations = db.Associations.AsNoTracking().Where(x => accessible.Contains(x.Id)
            && (x.Status == AssociationStatus.Active || x.Status == AssociationStatus.Paused));
        var associationRows = await associations.ToListAsync(cancellationToken);
        var ids = associationRows.Select(x => x.Id).ToArray();
        var memberships = await db.Memberships.AsNoTracking()
            .Where(x => ids.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var periods = await db.Periods.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var requirements = new Dictionary<(Guid PersonId, Guid AssociationId, Guid PeriodId), decimal>();
        foreach (var membership in memberships.Where(x => x.Kind == MembershipKind.Paying))
        {
            var association = associationRows.Single(x => x.Id == membership.AssociationId);
            var joined = DateOnly.FromDateTime(membership.JoinedAtUtc.UtcDateTime);
            var left = membership.LeftAtUtc.HasValue ? DateOnly.FromDateTime(membership.LeftAtUtc.Value.UtcDateTime) : (DateOnly?)null;
            foreach (var period in periods.Where(x => x.AssociationId == association.Id && x.PeriodDate >= joined
                         && (!left.HasValue || x.PeriodDate <= left.Value)))
            {
                var key = (membership.PersonId, association.Id, period.Id);
                requirements[key] = requirements.GetValueOrDefault(key) + membership.ShareCount * association.MonthlyContribution;
            }
        }
        var allocationRows = await db.PaymentAllocations.AsNoTracking().Where(x => ids.Contains(x.AssociationId))
            .Join(db.Payments.AsNoTracking(), allocation => allocation.PaymentId, payment => payment.Id,
                (allocation, payment) => new { payment.PersonId, allocation.AssociationId, allocation.PeriodId, allocation.AllocatedAmount })
            .ToListAsync(cancellationToken);
        var creditRows = await db.AssociationCredits.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var creditApplications = await db.AssociationCreditApplications.AsNoTracking()
            .Where(x => ids.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var totalDue = requirements.Sum(x => x.Value);
        var allocated = allocationRows.Sum(x => x.AllocatedAmount);
        var credited = creditRows.Sum(x => x.Amount);
        var totalPaid = allocated + credited;
        var active = await associations.CountAsync(x => x.Status == AssociationStatus.Active, cancellationToken);
        var people = memberships.Select(x => x.PersonId).Distinct().Count();
        var today = clock.Today;
        var currentPayouts = await db.Payouts.AsNoTracking().Where(x => ids.Contains(x.AssociationId) && x.Status == PayoutStatus.Scheduled)
            .Join(db.Periods, x => x.PeriodId, p => p.Id, (x, p) => p).CountAsync(x => x.PeriodDate.Year == today.Year && x.PeriodDate.Month == today.Month, cancellationToken);
        var outstandingPayments = 0;
        foreach (var group in requirements.GroupBy(x => new { x.Key.PersonId, x.Key.AssociationId }))
        {
            var availableCredit = creditRows.Where(x => x.PersonId == group.Key.PersonId && x.AssociationId == group.Key.AssociationId)
                    .Sum(x => x.Amount)
                - creditApplications.Where(x => x.PersonId == group.Key.PersonId && x.AssociationId == group.Key.AssociationId)
                    .Sum(x => x.Amount);
            foreach (var obligation in group.OrderBy(x => periods.Single(period => period.Id == x.Key.PeriodId).DueDate))
            {
                var period = periods.Single(x => x.Id == obligation.Key.PeriodId);
                var paid = allocationRows.Where(x => x.PersonId == group.Key.PersonId
                        && x.AssociationId == group.Key.AssociationId && x.PeriodId == obligation.Key.PeriodId)
                    .Sum(x => x.AllocatedAmount)
                    + creditApplications.Where(x => x.PersonId == group.Key.PersonId
                        && x.AssociationId == group.Key.AssociationId && x.PeriodId == obligation.Key.PeriodId).Sum(x => x.Amount);
                var remaining = Math.Max(0, obligation.Value - paid);
                var creditUsed = Math.Min(remaining, Math.Max(0, availableCredit));
                remaining -= creditUsed;
                availableCredit -= creditUsed;
                if (period.DueDate <= today && remaining > 0) outstandingPayments++;
            }
        }
        return new DashboardDto(totalDue, totalPaid, Math.Max(0, totalDue - totalPaid), active, people, currentPayouts,
            outstandingPayments);
    }
}
