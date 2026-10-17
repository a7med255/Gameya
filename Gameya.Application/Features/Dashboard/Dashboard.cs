using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using MediatR;

namespace Gameya.Application.Features.Dashboard;

public sealed record GetDashboardQuery(int Year, int Month) : IRequest<DashboardDto>;
public sealed class GetDashboardHandler(ICurrentUser current, IMonthlyFinanceService finance)
    : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    public async Task<DashboardDto> Handle(GetDashboardQuery request, CancellationToken cancellationToken)
    {
        var summary = await finance.GetAsync(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), request.Year, request.Month, cancellationToken);
        return new DashboardDto(summary.Year, summary.Month, summary.MonthLabel, summary.TotalRequired,
            summary.TotalPaid, summary.TotalRemaining, summary.TotalOverdue, summary.OverduePeopleCount,
            summary.CollectionProgress, summary.ActiveAssociations, summary.TotalPeople,
            summary.ScheduledPayoutCount);
    }
}

public sealed record GetMonthlyFinanceQuery(int Year, int Month) : IRequest<MonthlyFinanceDto>;
public sealed class GetMonthlyFinanceHandler(ICurrentUser current, IMonthlyFinanceService finance)
    : IRequestHandler<GetMonthlyFinanceQuery, MonthlyFinanceDto>
{
    public Task<MonthlyFinanceDto> Handle(GetMonthlyFinanceQuery request, CancellationToken cancellationToken) =>
        finance.GetAsync(current.UserId ?? throw new ForbiddenException(), current.IsInRole(AppRoles.Admin),
            request.Year, request.Month, cancellationToken);
}

public sealed record GetMonthlyCollectionsQuery(int Year, int Month, bool OverdueOnly, int Page = 1,
    int PageSize = 20) : IRequest<PagedResult<MonthlyCollectionPersonDto>>;
public sealed class GetMonthlyCollectionsHandler(ICurrentUser current, IMonthlyFinanceService finance)
    : IRequestHandler<GetMonthlyCollectionsQuery, PagedResult<MonthlyCollectionPersonDto>>
{
    public async Task<PagedResult<MonthlyCollectionPersonDto>> Handle(GetMonthlyCollectionsQuery request,
        CancellationToken cancellationToken)
    {
        var summary = await finance.GetAsync(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), request.Year, request.Month, cancellationToken);
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var source = request.OverdueOnly ? summary.Collections.Where(x => x.Overdue > 0).ToArray()
            : summary.Collections.ToArray();
        return new PagedResult<MonthlyCollectionPersonDto>(source.Skip((page - 1) * size).Take(size).ToArray(),
            page, size, source.Length);
    }
}

public sealed record GetMonthlyPayoutsQuery(int Year, int Month, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<MonthlyPayoutPersonDto>>;
public sealed class GetMonthlyPayoutsHandler(ICurrentUser current, IMonthlyFinanceService finance)
    : IRequestHandler<GetMonthlyPayoutsQuery, PagedResult<MonthlyPayoutPersonDto>>
{
    public async Task<PagedResult<MonthlyPayoutPersonDto>> Handle(GetMonthlyPayoutsQuery request,
        CancellationToken cancellationToken)
    {
        var summary = await finance.GetAsync(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), request.Year, request.Month, cancellationToken);
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        return new PagedResult<MonthlyPayoutPersonDto>(summary.PayoutRecipients.Skip((page - 1) * size)
            .Take(size).ToArray(), page, size, summary.PayoutRecipients.Count);
    }
}
