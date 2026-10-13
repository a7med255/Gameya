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
        var associations = db.Associations.AsNoTracking().Where(x => accessible.Contains(x.Id));
        var ids = await associations.Select(x => x.Id).ToArrayAsync(cancellationToken);
        var totalDue = await db.Memberships.AsNoTracking()
            .Where(x => ids.Contains(x.AssociationId) && x.Kind == MembershipKind.Paying)
            .Join(associations, m => m.AssociationId, a => a.Id, (m, a) => m.ShareCount * a.MonthlyContribution * a.NumberOfMonths)
            .SumAsync(cancellationToken);
        var allocated = await db.PaymentAllocations.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).SumAsync(x => x.AllocatedAmount, cancellationToken);
        var credited = await db.AssociationCredits.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).SumAsync(x => x.Amount, cancellationToken);
        var totalPaid = allocated + credited;
        var active = await associations.CountAsync(x => x.Status == AssociationStatus.Active, cancellationToken);
        var people = await db.Memberships.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).Select(x => x.PersonId).Distinct().CountAsync(cancellationToken);
        var today = clock.Today;
        var currentPayouts = await db.Payouts.AsNoTracking().Where(x => ids.Contains(x.AssociationId) && x.Status == PayoutStatus.Scheduled)
            .Join(db.Periods, x => x.PeriodId, p => p.Id, (x, p) => p).CountAsync(x => x.PeriodDate.Year == today.Year && x.PeriodDate.Month == today.Month, cancellationToken);
        return new DashboardDto(totalDue, totalPaid, Math.Max(0, totalDue - totalPaid), active, people, currentPayouts,
            totalDue > totalPaid ? 1 : 0);
    }
}
