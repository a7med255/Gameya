using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Associations;

public sealed record GetAssociationAuditQuery(Guid AssociationId, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AssociationAuditDto>>;

public sealed class GetAssociationAuditHandler(
    IApplicationDbContext db,
    IAssociationAccessService access,
    IUserDisplayNameService users) : IRequestHandler<GetAssociationAuditQuery, PagedResult<AssociationAuditDto>>
{
    public async Task<PagedResult<AssociationAuditDto>> Handle(GetAssociationAuditQuery request,
        CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var associationId = request.AssociationId.ToString();
        var membershipIds = await db.Memberships.AsNoTracking().Where(x => x.AssociationId == request.AssociationId)
            .Select(x => x.Id.ToString()).ToArrayAsync(cancellationToken);
        var periodIds = await db.Periods.AsNoTracking().Where(x => x.AssociationId == request.AssociationId)
            .Select(x => x.Id.ToString()).ToArrayAsync(cancellationToken);
        var payoutIds = await db.Payouts.AsNoTracking().Where(x => x.AssociationId == request.AssociationId)
            .Select(x => x.Id.ToString()).ToArrayAsync(cancellationToken);
        var replacementIds = await db.Replacements.AsNoTracking().Where(x => x.AssociationId == request.AssociationId)
            .Select(x => x.Id.ToString()).ToArrayAsync(cancellationToken);
        var relatedIds = membershipIds.Concat(periodIds).Concat(payoutIds).Concat(replacementIds)
            .Append(associationId).ToArray();

        var query = db.AuditLogs.AsNoTracking().Where(x => relatedIds.Contains(x.EntityId)
            || (x.OldValues != null && x.OldValues.Contains(associationId))
            || (x.NewValues != null && x.NewValues.Contains(associationId)));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.TimestampUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Action, x.EntityName, x.EntityId, x.UserId, x.OldValues, x.NewValues, x.TimestampUtc
            }).ToListAsync(cancellationToken);
        var actorNames = await users.GetAsync(rows.Where(x => x.UserId != null).Select(x => x.UserId!)
            .Distinct().ToArray(), cancellationToken);
        var items = rows.Select(x => new AssociationAuditDto(x.Id, x.Action, x.EntityName, x.EntityId,
            x.UserId, x.UserId is not null ? actorNames.GetValueOrDefault(x.UserId, x.UserId) : "النظام",
            x.OldValues, x.NewValues, x.TimestampUtc)).ToArray();
        return new PagedResult<AssociationAuditDto>(items, page, pageSize, total);
    }
}
