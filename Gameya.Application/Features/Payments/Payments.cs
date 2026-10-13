using FluentValidation;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Payments;

public sealed record RecordPaymentCommand(Guid PersonId, decimal Amount, PaymentMethod Method,
    IReadOnlyCollection<Guid>? AssociationIds, Guid? CreditAssociationId) : IRequest<PaymentDto>;
public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithErrorCode("person.required");
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, false).WithErrorCode("payment.invalid_amount");
        RuleFor(x => x.Method).IsInEnum().WithErrorCode("payment.invalid_method");
    }
}

public sealed class RecordPaymentHandler(IApplicationDbContext db, ICurrentUser current,
    IAssociationAccessService access, IClock clock, IAuditService audit) : IRequestHandler<RecordPaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(RecordPaymentCommand request, CancellationToken cancellationToken) =>
        db.ExecuteSerializableAsync(async innerCt =>
        {
            var now = clock.UtcNow;
            var personExists = await db.People.AnyAsync(x => x.Id == request.PersonId && x.IsActive, innerCt);
            if (!personExists) throw new NotFoundException("person.not_found", "Person was not found.");

            var accessibleIdsQuery = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
                current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
            var requestedIds = request.AssociationIds?.Distinct().ToArray() ?? [];
            var associationIds = requestedIds.Length == 0
                ? await db.Memberships.Where(x => x.PersonId == request.PersonId && accessibleIdsQuery.Contains(x.AssociationId))
                    .Select(x => x.AssociationId).Distinct().ToArrayAsync(innerCt)
                : await accessibleIdsQuery.Where(x => requestedIds.Contains(x)).ToArrayAsync(innerCt);

            if (requestedIds.Length != 0 && associationIds.Length != requestedIds.Length)
                throw new NotFoundException("association.not_found", "One or more associations were not found.");
            if (associationIds.Length == 0) throw new NotFoundException("payment.no_accessible_associations", "No accessible associations were found for this person.");
            if (request.CreditAssociationId.HasValue && !associationIds.Contains(request.CreditAssociationId.Value))
                throw new NotFoundException("association.not_found", "Association was not found.");

            var membershipData = await db.Memberships.AsNoTracking()
                .Where(m => m.PersonId == request.PersonId && associationIds.Contains(m.AssociationId) && m.Kind == MembershipKind.Paying)
                .Join(db.Associations.AsNoTracking(), m => m.AssociationId, a => a.Id, (m, a) => new
                {
                    Membership = m, Association = a
                }).ToListAsync(innerCt);
            var validAssociationIds = membershipData.Select(x => x.Association.Id).Distinct().ToArray();
            if (validAssociationIds.Length != associationIds.Length)
                throw new NotFoundException("membership.not_found", "The person is not a paying member of one or more selected associations.");

            var periods = await db.Periods.AsNoTracking()
                .Where(x => validAssociationIds.Contains(x.AssociationId) && x.DueDate <= clock.Today)
                .OrderBy(x => x.DueDate).ThenBy(x => x.PeriodNumber).ToListAsync(innerCt);
            var existingAllocations = await db.PaymentAllocations.AsNoTracking()
                .Where(x => validAssociationIds.Contains(x.AssociationId))
                .GroupBy(x => new { x.AssociationId, x.PeriodId })
                .Select(x => new { x.Key.AssociationId, x.Key.PeriodId, Amount = x.Sum(y => y.AllocatedAmount) })
                .ToListAsync(innerCt);
            var credits = await db.AssociationCredits.AsNoTracking()
                .Where(x => x.PersonId == request.PersonId && validAssociationIds.Contains(x.AssociationId))
                .GroupBy(x => x.AssociationId).Select(x => new { AssociationId = x.Key, Amount = x.Sum(c => c.Amount) })
                .ToDictionaryAsync(x => x.AssociationId, x => x.Amount, innerCt);
            var usedCredits = await db.AssociationCreditApplications.AsNoTracking()
                .Where(x => x.PersonId == request.PersonId && validAssociationIds.Contains(x.AssociationId))
                .GroupBy(x => new { x.AssociationId, x.PeriodId })
                .Select(x => new { x.Key.AssociationId, x.Key.PeriodId, Amount = x.Sum(c => c.Amount) })
                .ToListAsync(innerCt);

            var obligations = new List<Obligation>();
            foreach (var item in membershipData)
            {
                var joined = DateOnly.FromDateTime(item.Membership.JoinedAtUtc.UtcDateTime);
                var left = item.Membership.LeftAtUtc.HasValue ? DateOnly.FromDateTime(item.Membership.LeftAtUtc.Value.UtcDateTime) : (DateOnly?)null;
                foreach (var period in periods.Where(x => x.AssociationId == item.Association.Id && x.PeriodDate >= joined && (!left.HasValue || x.PeriodDate <= left.Value)))
                {
                    var required = item.Association.MonthlyContribution * item.Membership.ShareCount;
                    var allocated = existingAllocations.Where(x => x.AssociationId == period.AssociationId && x.PeriodId == period.Id).Sum(x => x.Amount)
                        + usedCredits.Where(x => x.AssociationId == period.AssociationId && x.PeriodId == period.Id).Sum(x => x.Amount);
                    obligations.Add(new Obligation(item.Association.Id, period.Id, period.DueDate, Math.Max(0, required - allocated)));
                }
            }

            // Existing association credit offsets the oldest obligation in that association without becoming global credit.
            foreach (var associationId in validAssociationIds)
            {
                var availableCredit = credits.GetValueOrDefault(associationId)
                    - usedCredits.Where(x => x.AssociationId == associationId).Sum(x => x.Amount);
                foreach (var obligation in obligations.Where(x => x.AssociationId == associationId).OrderBy(x => x.DueDate))
                {
                    var used = Math.Min(obligation.Remaining, availableCredit);
                    if (used > 0)
                    {
                        await db.AddEntityAsync(new AssociationCreditApplication(request.PersonId, associationId,
                            obligation.PeriodId, used, now), innerCt);
                        obligation.Remaining -= used; availableCredit -= used;
                    }
                    if (availableCredit == 0) break;
                }
            }

            var payment = new Payment(request.PersonId, current.UserId!, request.Amount, request.Method, now, now);
            var unallocated = request.Amount;
            var orderedObligations = obligations.Where(x => x.Remaining > 0).GroupBy(x => x.AssociationId)
                .OrderBy(x => x.Min(o => o.DueDate)).ThenBy(x => x.Key)
                .SelectMany(x => x.OrderBy(o => o.DueDate));
            foreach (var obligation in orderedObligations)
            {
                if (unallocated == 0) break;
                var amount = Math.Min(unallocated, obligation.Remaining);
                payment.Allocate(obligation.AssociationId, obligation.PeriodId, amount, obligation.DueDate, clock.Today);
                unallocated -= amount;
            }
            await db.AddEntityAsync(payment, innerCt);
            if (unallocated > 0)
            {
                if (!request.CreditAssociationId.HasValue)
                    throw new ConflictException("payment.credit_association_required", "Choose an association for the unallocated credit.");
                await db.AddEntityAsync(new AssociationCredit(payment.Id, request.PersonId, request.CreditAssociationId.Value, unallocated, now), innerCt);
            }
            await db.SaveChangesAsync(innerCt);
            await audit.WriteAsync("PaymentRecorded", nameof(Payment), payment.Id.ToString(), null,
                new { request.PersonId, request.Amount, request.Method, Allocations = payment.Allocations.Count, Credit = unallocated }, innerCt);
            return new PaymentDto(payment.Id, payment.PersonId, payment.Amount, payment.Method, payment.PaidAtUtc,
                payment.EntryType, payment.Allocations.Select(x => new PaymentAllocationDto(x.AssociationId, x.PeriodId,
                    x.AllocatedAmount, x.DueDate, x.LateDays)).ToList(), unallocated > 0
                    ? [new PaymentCreditDto(request.CreditAssociationId!.Value, unallocated)] : []);
        }, cancellationToken);

    private sealed record Obligation(Guid AssociationId, Guid PeriodId, DateOnly DueDate, decimal InitialRemaining)
    {
        public decimal Remaining { get; set; } = InitialRemaining;
    }
}

public sealed record AdjustmentAllocationInput(Guid AssociationId, Guid PeriodId, decimal Amount);
public sealed record AdjustmentCreditInput(Guid AssociationId, decimal Amount);
public sealed record AdjustPaymentCommand(Guid PaymentId, decimal Amount, string Reason,
    IReadOnlyCollection<AdjustmentAllocationInput> Allocations, IReadOnlyCollection<AdjustmentCreditInput> Credits) : IRequest<Guid>;
public sealed class AdjustPaymentValidator : AbstractValidator<AdjustPaymentCommand>
{
    public AdjustPaymentValidator()
    {
        RuleFor(x => x.Amount).NotEqual(0).WithErrorCode("payment.zero_amount");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithErrorCode("payment.adjustment_reason");
        RuleFor(x => x).Must(x => x.Allocations.Sum(a => a.Amount) + x.Credits.Sum(c => c.Amount) == x.Amount)
            .WithErrorCode("payment.adjustment_total_mismatch");
    }
}
public sealed class AdjustPaymentHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access,
    IClock clock, IAuditService audit)
    : IRequestHandler<AdjustPaymentCommand, Guid>
{
    public async Task<Guid> Handle(AdjustPaymentCommand request, CancellationToken cancellationToken)
    {
        var original = await db.Payments.AsNoTracking().Include(x => x.Allocations)
            .SingleOrDefaultAsync(x => x.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException("payment.not_found", "Payment was not found.");
        var originalCredits = await db.AssociationCredits.AsNoTracking().Where(x => x.PaymentId == original.Id).ToListAsync(cancellationToken);
        foreach (var allocation in request.Allocations)
        {
            await access.EnsureAccessAsync(allocation.AssociationId, cancellationToken);
            if (!original.Allocations.Any(x => x.AssociationId == allocation.AssociationId && x.PeriodId == allocation.PeriodId))
                throw new ConflictException("payment.adjustment_scope", "An adjustment can only target the original payment entries.");
        }
        foreach (var credit in request.Credits)
        {
            await access.EnsureAccessAsync(credit.AssociationId, cancellationToken);
            if (!originalCredits.Any(x => x.AssociationId == credit.AssociationId))
                throw new ConflictException("payment.adjustment_scope", "An adjustment can only target the original payment entries.");
        }
        var now = clock.UtcNow;
        var adjustment = new Payment(original.PersonId, current.UserId ?? throw new ForbiddenException(), request.Amount,
            original.Method, now, now, PaymentEntryType.Adjustment, original.Id);
        foreach (var item in request.Allocations)
        {
            var dueDate = original.Allocations.First(x => x.AssociationId == item.AssociationId && x.PeriodId == item.PeriodId).DueDate;
            adjustment.AllocateAdjustment(item.AssociationId, item.PeriodId, item.Amount, dueDate);
        }
        await db.AddEntityAsync(adjustment, cancellationToken);
        foreach (var item in request.Credits)
            await db.AddEntityAsync(new AssociationCredit(adjustment.Id, original.PersonId, item.AssociationId, item.Amount, now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PaymentAdjusted", nameof(Payment), adjustment.Id.ToString(),
            new { OriginalPaymentId = original.Id }, new { request.Amount, request.Reason }, cancellationToken);
        return adjustment.Id;
    }
}

public sealed record GetPaymentByIdQuery(Guid PaymentId) : IRequest<PaymentDto>;
public sealed class GetPaymentByIdHandler(IApplicationDbContext db, IAssociationAccessService access, ICurrentUser current)
    : IRequestHandler<GetPaymentByIdQuery, PaymentDto>
{
    public async Task<PaymentDto> Handle(GetPaymentByIdQuery request, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().Include(x => x.Allocations)
            .SingleOrDefaultAsync(x => x.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException("payment.not_found", "Payment was not found.");
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var allowedIds = await accessible.ToArrayAsync(cancellationToken);
        var allocations = payment.Allocations.Where(x => allowedIds.Contains(x.AssociationId))
            .Select(x => new PaymentAllocationDto(x.AssociationId, x.PeriodId, x.AllocatedAmount, x.DueDate, x.LateDays)).ToList();
        var credits = await db.AssociationCredits.AsNoTracking().Where(x => x.PaymentId == payment.Id && allowedIds.Contains(x.AssociationId))
            .Select(x => new PaymentCreditDto(x.AssociationId, x.Amount)).ToListAsync(cancellationToken);
        if (allocations.Count == 0 && credits.Count == 0) throw new NotFoundException("payment.not_found", "Payment was not found.");
        return new PaymentDto(payment.Id, payment.PersonId, allocations.Sum(x => x.Amount) + credits.Sum(x => x.Amount),
            payment.Method, payment.PaidAtUtc, payment.EntryType, allocations, credits);
    }
}

public sealed record GetPaymentsQuery(Guid? PersonId, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PaymentDto>>;
public sealed class GetPaymentsHandler(IApplicationDbContext db, IAssociationAccessService access, ICurrentUser current)
    : IRequestHandler<GetPaymentsQuery, PagedResult<PaymentDto>>
{
    public async Task<PagedResult<PaymentDto>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var allocationPaymentIds = db.PaymentAllocations.Where(x => accessible.Contains(x.AssociationId)).Select(x => x.PaymentId);
        var creditPaymentIds = db.AssociationCredits.Where(x => accessible.Contains(x.AssociationId)).Select(x => x.PaymentId);
        var visiblePaymentIds = allocationPaymentIds.Concat(creditPaymentIds).Distinct();
        var query = db.Payments.AsNoTracking().Where(x => visiblePaymentIds.Contains(x.Id));
        if (request.PersonId.HasValue) query = query.Where(x => x.PersonId == request.PersonId.Value);
        var page = Math.Max(1, request.Page); var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var payments = await query.OrderByDescending(x => x.PaidAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.Allocations).ToListAsync(cancellationToken);
        var paymentIds = payments.Select(x => x.Id).ToArray();
        var allowedIds = await accessible.ToArrayAsync(cancellationToken);
        var credits = await db.AssociationCredits.AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId)
            && allowedIds.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var items = payments.Select(payment =>
        {
            var allocations = payment.Allocations.Where(x => allowedIds.Contains(x.AssociationId))
                .Select(x => new PaymentAllocationDto(x.AssociationId, x.PeriodId, x.AllocatedAmount, x.DueDate, x.LateDays)).ToList();
            var paymentCredits = credits.Where(x => x.PaymentId == payment.Id)
                .Select(x => new PaymentCreditDto(x.AssociationId, x.Amount)).ToList();
            return new PaymentDto(payment.Id, payment.PersonId, allocations.Sum(x => x.Amount) + paymentCredits.Sum(x => x.Amount),
                payment.Method, payment.PaidAtUtc, payment.EntryType, allocations, paymentCredits);
        }).ToList();
        return new PagedResult<PaymentDto>(items, page, pageSize, total);
    }
}

public sealed record GetPaymentPageQuery(Guid PersonId) : IRequest<PaymentPageDto>;
public sealed class GetPaymentPageHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access, IClock clock)
    : IRequestHandler<GetPaymentPageQuery, PaymentPageDto>
{
    public async Task<PaymentPageDto> Handle(GetPaymentPageQuery request, CancellationToken cancellationToken)
    {
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PersonId, cancellationToken)
            ?? throw new NotFoundException("person.not_found", "Person was not found.");
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var membershipData = await db.Memberships.AsNoTracking()
            .Where(x => x.PersonId == person.Id && x.Kind == MembershipKind.Paying && accessible.Contains(x.AssociationId))
            .Join(db.Associations.AsNoTracking(), m => m.AssociationId, a => a.Id, (m, a) => new { m, a }).ToListAsync(cancellationToken);
        var ids = membershipData.Select(x => x.a.Id).Distinct().ToArray();
        var allocations = await db.PaymentAllocations.AsNoTracking().Where(x => ids.Contains(x.AssociationId))
            .GroupBy(x => x.AssociationId).Select(x => new { Id = x.Key, Amount = x.Sum(a => a.AllocatedAmount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var credits = await db.AssociationCredits.AsNoTracking().Where(x => x.PersonId == person.Id && ids.Contains(x.AssociationId))
            .GroupBy(x => x.AssociationId).Select(x => new { Id = x.Key, Amount = x.Sum(c => c.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var creditApplications = await db.AssociationCreditApplications.AsNoTracking()
            .Where(x => x.PersonId == person.Id && ids.Contains(x.AssociationId))
            .GroupBy(x => x.AssociationId).Select(x => new { Id = x.Key, Amount = x.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var today = clock.Today;
        var periods = await db.Periods.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var result = new List<AccessibleAssociationDueDto>();
        foreach (var group in membershipData.GroupBy(x => x.a.Id))
        {
            var association = group.First().a;
            var required = group.Sum(x => x.m.ShareCount) * association.MonthlyContribution * association.NumberOfMonths;
            var paid = allocations.GetValueOrDefault(association.Id);
            var totalCredit = credits.GetValueOrDefault(association.Id);
            var availableCredit = totalCredit - creditApplications.GetValueOrDefault(association.Id);
            var remaining = Math.Max(0, required - paid - totalCredit);
            var oldest = periods.Where(x => x.AssociationId == association.Id && x.DueDate <= today)
                .OrderBy(x => x.DueDate).Select(x => (DateOnly?)x.DueDate).FirstOrDefault();
            result.Add(new AccessibleAssociationDueDto(association.Id, association.Name, association.MonthlyContribution,
                remaining, oldest, availableCredit, remaining == 0 ? "Paid" : oldest.HasValue ? "Outstanding" : "Upcoming"));
        }
        return new PaymentPageDto(person.Id, person.FullName, person.SubscriberCode.Value, result, result.Sum(x => x.OutstandingAmount));
    }
}
