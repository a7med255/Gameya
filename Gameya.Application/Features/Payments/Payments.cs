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
    IReadOnlyCollection<Guid>? AssociationIds = null, Guid? CreditAssociationId = null,
    string? IdempotencyKey = null) : IRequest<PaymentDto>;
public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithErrorCode("person.required");
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, false).WithErrorCode("payment.invalid_amount");
        RuleFor(x => x.Method).IsInEnum().WithErrorCode("payment.invalid_method");
        RuleFor(x => x.IdempotencyKey).MaximumLength(100).WithErrorCode("payment.invalid_idempotency_key");
    }
}

public sealed class RecordPaymentHandler(IApplicationDbContext db, ICurrentUser current,
    IAssociationAccessService access, IClock clock, IAuditService audit) : IRequestHandler<RecordPaymentCommand, PaymentDto>
{
    public Task<PaymentDto> Handle(RecordPaymentCommand request, CancellationToken cancellationToken) =>
        db.ExecuteSerializableAsync(async innerCt =>
        {
            var now = clock.UtcNow;
            var recorderId = current.UserId ?? throw new ForbiddenException();
            if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
            {
                var existingPayment = await db.Payments.AsNoTracking().Include(x => x.Allocations)
                    .SingleOrDefaultAsync(x => x.RecordedByUserId == recorderId
                        && x.IdempotencyKey == request.IdempotencyKey.Trim(), innerCt);
                if (existingPayment is not null)
                {
                    var existingCredits = await db.AssociationCredits.AsNoTracking()
                        .Where(x => x.PaymentId == existingPayment.Id)
                        .Select(x => new PaymentCreditDto(x.AssociationId, x.Amount)).ToListAsync(innerCt);
                    return new PaymentDto(existingPayment.Id, existingPayment.PersonId, existingPayment.Amount,
                        existingPayment.Method, existingPayment.PaidAtUtc, existingPayment.EntryType,
                        existingPayment.Allocations.Select(x => new PaymentAllocationDto(x.AssociationId, x.PeriodId,
                            x.AllocatedAmount, x.DueDate, x.LateDays)).ToList(), existingCredits);
                }
            }
            var accessibleIdsQuery = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
                current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
            var associationIds = await db.Memberships
                .Where(x => x.PersonId == request.PersonId && accessibleIdsQuery.Contains(x.AssociationId))
                .Join(db.Associations.Where(x => x.Status == AssociationStatus.Active || x.Status == AssociationStatus.Paused),
                    membership => membership.AssociationId, association => association.Id, (_, association) => association.Id)
                .Distinct().ToArrayAsync(innerCt);
            if (associationIds.Length == 0) throw new NotFoundException("payment.no_accessible_associations", "No accessible associations were found for this person.");

            var membershipData = await db.Memberships.AsNoTracking()
                .Where(m => m.PersonId == request.PersonId && associationIds.Contains(m.AssociationId) && m.Kind == MembershipKind.Paying)
                .Join(db.Associations.AsNoTracking().Where(a => a.Status == AssociationStatus.Active || a.Status == AssociationStatus.Paused),
                    m => m.AssociationId, a => a.Id, (m, a) => new
                {
                    Membership = m, Association = a
                }).ToListAsync(innerCt);
            var validAssociationIds = membershipData.Select(x => x.Association.Id).Distinct().ToArray();
            if (validAssociationIds.Length != associationIds.Length)
                throw new NotFoundException("person.not_found", "Person was not found.");
            if (!await db.People.AnyAsync(x => x.Id == request.PersonId && x.IsActive, innerCt))
                throw new NotFoundException("person.not_found", "Person was not found.");

            var allPeriods = await db.Periods.AsNoTracking()
                .Where(x => validAssociationIds.Contains(x.AssociationId))
                .OrderBy(x => x.DueDate).ThenBy(x => x.PeriodNumber).ToListAsync(innerCt);
            var periods = allPeriods.Where(x => x.PeriodDate <= clock.Today).ToList();
            var existingAllocations = await db.PaymentAllocations.AsNoTracking()
                .Where(x => validAssociationIds.Contains(x.AssociationId))
                .Join(db.Payments.AsNoTracking().Where(x => x.PersonId == request.PersonId),
                    allocation => allocation.PaymentId, payment => payment.Id, (allocation, _) => allocation)
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

            var requiredByPeriod = new Dictionary<(Guid AssociationId, Guid PeriodId), (DateOnly DueDate, decimal Amount)>();
            foreach (var item in membershipData)
            {
                var joined = DateOnly.FromDateTime(item.Membership.JoinedAtUtc.UtcDateTime);
                var left = item.Membership.LeftAtUtc.HasValue ? DateOnly.FromDateTime(item.Membership.LeftAtUtc.Value.UtcDateTime) : (DateOnly?)null;
                foreach (var period in periods.Where(x => x.AssociationId == item.Association.Id && x.PeriodDate >= joined && (!left.HasValue || x.PeriodDate <= left.Value)))
                {
                    var key = (item.Association.Id, period.Id);
                    var required = item.Association.MonthlyContribution * item.Membership.ShareCount;
                    requiredByPeriod[key] = requiredByPeriod.TryGetValue(key, out var existing)
                        ? (period.DueDate, existing.Amount + required)
                        : (period.DueDate, required);
                }
            }
            var obligations = requiredByPeriod.Select(item =>
            {
                var allocated = existingAllocations.Where(x => x.AssociationId == item.Key.AssociationId && x.PeriodId == item.Key.PeriodId).Sum(x => x.Amount)
                    + usedCredits.Where(x => x.AssociationId == item.Key.AssociationId && x.PeriodId == item.Key.PeriodId).Sum(x => x.Amount);
                return new Obligation(item.Key.AssociationId, item.Key.PeriodId, item.Value.DueDate,
                    Math.Max(0, item.Value.Amount - allocated));
            }).ToList();

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

            var payment = new Payment(request.PersonId, recorderId, request.Amount, request.Method, now, now,
                idempotencyKey: request.IdempotencyKey);
            var unallocated = request.Amount;
            var orderedObligations = obligations.Where(x => x.Remaining > 0)
                .OrderBy(x => x.DueDate).ThenBy(x => x.AssociationId).ThenBy(x => x.PeriodId);
            foreach (var obligation in orderedObligations)
            {
                if (unallocated == 0) break;
                var amount = Math.Min(unallocated, obligation.Remaining);
                payment.Allocate(obligation.AssociationId, obligation.PeriodId, amount, obligation.DueDate, clock.Today);
                unallocated -= amount;
            }
            await db.AddEntityAsync(payment, innerCt);
            var creditAssociationId = allPeriods
                .Where(period => period.DueDate > clock.Today && membershipData.Any(item =>
                    item.Association.Id == period.AssociationId
                    && period.PeriodDate >= DateOnly.FromDateTime(item.Membership.JoinedAtUtc.UtcDateTime)
                    && (!item.Membership.LeftAtUtc.HasValue
                        || period.PeriodDate <= DateOnly.FromDateTime(item.Membership.LeftAtUtc.Value.UtcDateTime))))
                .OrderBy(period => period.DueDate).ThenBy(period => period.AssociationId)
                .Select(period => (Guid?)period.AssociationId).FirstOrDefault()
                ?? payment.Allocations.LastOrDefault()?.AssociationId
                ?? validAssociationIds.OrderBy(id => id).First();
            if (unallocated > 0)
            {
                await db.AddEntityAsync(new AssociationCredit(payment.Id, request.PersonId, creditAssociationId, unallocated, now), innerCt);
            }
            await db.SaveChangesAsync(innerCt);
            await audit.WriteAsync("PaymentRecorded", nameof(Payment), payment.Id.ToString(), null,
                new { request.PersonId, request.Amount, request.Method, Allocations = payment.Allocations.Count, Credit = unallocated }, innerCt);
            return new PaymentDto(payment.Id, payment.PersonId, payment.Amount, payment.Method, payment.PaidAtUtc,
                payment.EntryType, payment.Allocations.Select(x => new PaymentAllocationDto(x.AssociationId, x.PeriodId,
                    x.AllocatedAmount, x.DueDate, x.LateDays)).ToList(), unallocated > 0
                    ? [new PaymentCreditDto(creditAssociationId, unallocated)] : []);
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
        RuleFor(x => x.Amount).PrecisionScale(18, 2, false).WithErrorCode("payment.invalid_amount");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500).WithErrorCode("payment.adjustment_reason");
        RuleFor(x => x.Allocations).NotNull().WithErrorCode("payment.adjustment_entries_required");
        RuleFor(x => x.Credits).NotNull().WithErrorCode("payment.adjustment_entries_required");
        RuleForEach(x => x.Allocations).ChildRules(x => x.RuleFor(y => y.Amount).NotEqual(0)
            .WithErrorCode("payment.zero_amount"));
        RuleForEach(x => x.Credits).ChildRules(x => x.RuleFor(y => y.Amount).NotEqual(0)
            .WithErrorCode("payment.zero_amount"));
        RuleFor(x => x.Allocations).Must(x => x is not null && x.GroupBy(y => new { y.AssociationId, y.PeriodId }).All(g => g.Count() == 1))
            .WithErrorCode("payment.duplicate_adjustment_entry");
        RuleFor(x => x.Credits).Must(x => x is not null && x.GroupBy(y => y.AssociationId).All(g => g.Count() == 1))
            .WithErrorCode("payment.duplicate_adjustment_entry");
        RuleFor(x => x).Must(x => x.Allocations is not null && x.Credits is not null
                && x.Allocations.Sum(a => a.Amount) + x.Credits.Sum(c => c.Amount) == x.Amount)
            .WithErrorCode("payment.adjustment_total_mismatch");
    }
}
public sealed class AdjustPaymentHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access,
    IClock clock, IAuditService audit)
    : IRequestHandler<AdjustPaymentCommand, Guid>
{
    public Task<Guid> Handle(AdjustPaymentCommand request, CancellationToken cancellationToken) =>
        db.ExecuteSerializableAsync(async innerCt =>
        {
            var original = await db.Payments.AsNoTracking().Include(x => x.Allocations)
                .SingleOrDefaultAsync(x => x.Id == request.PaymentId && x.EntryType == PaymentEntryType.Payment, innerCt)
                ?? throw new NotFoundException("payment.not_found", "Payment was not found.");
            var originalCredits = await db.AssociationCredits.AsNoTracking()
                .Where(x => x.PaymentId == original.Id).ToListAsync(innerCt);
            var originalAssociationIds = original.Allocations.Select(x => x.AssociationId)
                .Concat(originalCredits.Select(x => x.AssociationId)).Distinct().ToArray();
            var accessibleIds = await access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
                    current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner))
                .Where(x => originalAssociationIds.Contains(x)).ToArrayAsync(innerCt);
            if (accessibleIds.Length != originalAssociationIds.Length)
                throw new NotFoundException("payment.not_found", "Payment was not found.");

            foreach (var allocation in request.Allocations)
                if (!original.Allocations.Any(x => x.AssociationId == allocation.AssociationId && x.PeriodId == allocation.PeriodId))
                    throw new ConflictException("payment.adjustment_scope", "An adjustment can only target the original payment entries.");
            foreach (var credit in request.Credits)
                if (!originalCredits.Any(x => x.AssociationId == credit.AssociationId))
                    throw new ConflictException("payment.adjustment_scope", "An adjustment can only target the original payment entries.");

            var priorAdjustments = db.Payments.AsNoTracking().Where(x => x.AdjustsPaymentId == original.Id);
            var priorAdjustmentTotal = await priorAdjustments.SumAsync(x => x.Amount, innerCt);
            if (original.Amount + priorAdjustmentTotal + request.Amount < 0)
                throw new ConflictException("payment.adjustment_exceeds_original", "Adjustments cannot make the corrected payment negative.");
            var priorAdjustmentIds = priorAdjustments.Select(x => x.Id);
            var priorAllocations = await db.PaymentAllocations.AsNoTracking()
                .Where(x => priorAdjustmentIds.Contains(x.PaymentId)).ToListAsync(innerCt);
            var priorCredits = await db.AssociationCredits.AsNoTracking()
                .Where(x => priorAdjustmentIds.Contains(x.PaymentId)).ToListAsync(innerCt);
            foreach (var item in request.Allocations)
            {
                var originalAmount = original.Allocations.Where(x => x.AssociationId == item.AssociationId && x.PeriodId == item.PeriodId)
                    .Sum(x => x.AllocatedAmount);
                var priorAmount = priorAllocations.Where(x => x.AssociationId == item.AssociationId && x.PeriodId == item.PeriodId)
                    .Sum(x => x.AllocatedAmount);
                if (originalAmount + priorAmount + item.Amount < 0)
                    throw new ConflictException("payment.adjustment_exceeds_original", "An adjustment cannot make a payment entry negative.");
            }
            foreach (var item in request.Credits)
            {
                var originalAmount = originalCredits.Where(x => x.AssociationId == item.AssociationId).Sum(x => x.Amount);
                var priorAmount = priorCredits.Where(x => x.AssociationId == item.AssociationId).Sum(x => x.Amount);
                if (originalAmount + priorAmount + item.Amount < 0)
                    throw new ConflictException("payment.adjustment_exceeds_original", "An adjustment cannot make a credit entry negative.");
                var associationCredit = await db.AssociationCredits.AsNoTracking()
                    .Where(x => x.PersonId == original.PersonId && x.AssociationId == item.AssociationId)
                    .SumAsync(x => x.Amount, innerCt);
                var appliedCredit = await db.AssociationCreditApplications.AsNoTracking()
                    .Where(x => x.PersonId == original.PersonId && x.AssociationId == item.AssociationId)
                    .SumAsync(x => x.Amount, innerCt);
                if (associationCredit + item.Amount < appliedCredit)
                    throw new ConflictException("payment.credit_already_applied", "Applied association credit cannot be reversed.");
            }

            var now = clock.UtcNow;
            var adjustment = new Payment(original.PersonId, current.UserId!, request.Amount,
                original.Method, now, now, PaymentEntryType.Adjustment, original.Id);
            foreach (var item in request.Allocations)
            {
                var dueDate = original.Allocations.First(x => x.AssociationId == item.AssociationId && x.PeriodId == item.PeriodId).DueDate;
                adjustment.AllocateAdjustment(item.AssociationId, item.PeriodId, item.Amount, dueDate);
            }
            await db.AddEntityAsync(adjustment, innerCt);
            foreach (var item in request.Credits)
                await db.AddEntityAsync(new AssociationCredit(adjustment.Id, original.PersonId, item.AssociationId, item.Amount, now), innerCt);
            await db.SaveChangesAsync(innerCt);
            await audit.WriteAsync("PaymentAdjusted", nameof(Payment), adjustment.Id.ToString(),
                new { OriginalPaymentId = original.Id }, new { request.Amount, request.Reason }, innerCt);
            return adjustment.Id;
        }, cancellationToken);
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

public sealed record GetPaymentsQuery(Guid? PersonId, DateOnly? FromDate, DateOnly? ToDate,
    PaymentMethod? Method, Guid? AssociationId, int? PeriodNumber, PaymentEntryType? EntryType,
    string? PayerSearch, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PaymentListItemDto>>;
public sealed class GetPaymentsHandler(IApplicationDbContext db, IAssociationAccessService access, ICurrentUser current,
    IClock clock)
    : IRequestHandler<GetPaymentsQuery, PagedResult<PaymentListItemDto>>
{
    public async Task<PagedResult<PaymentListItemDto>> Handle(GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var allocationPaymentIds = db.PaymentAllocations.Where(x => accessible.Contains(x.AssociationId)).Select(x => x.PaymentId);
        var creditPaymentIds = db.AssociationCredits.Where(x => accessible.Contains(x.AssociationId)).Select(x => x.PaymentId);
        var visiblePaymentIds = allocationPaymentIds.Concat(creditPaymentIds).Distinct();
        var query = db.Payments.AsNoTracking().Where(x => visiblePaymentIds.Contains(x.Id));
        if (request.PersonId.HasValue) query = query.Where(x => x.PersonId == request.PersonId.Value);
        if (request.Method.HasValue) query = query.Where(x => x.Method == request.Method.Value);
        if (request.EntryType.HasValue) query = query.Where(x => x.EntryType == request.EntryType.Value);
        if (request.FromDate.HasValue)
        {
            var start = clock.EgyptStartOfDayUtc(request.FromDate.Value);
            query = query.Where(x => x.PaidAtUtc >= start);
        }
        if (request.ToDate.HasValue)
        {
            var end = clock.EgyptStartOfDayUtc(request.ToDate.Value.AddDays(1));
            query = query.Where(x => x.PaidAtUtc < end);
        }
        if (request.AssociationId.HasValue)
            query = query.Where(x => x.Allocations.Any(a => a.AssociationId == request.AssociationId.Value)
                || db.AssociationCredits.Any(c => c.PaymentId == x.Id && c.AssociationId == request.AssociationId.Value));
        if (request.PeriodNumber.HasValue)
        {
            var periodIds = db.Periods.Where(x => x.PeriodNumber == request.PeriodNumber.Value).Select(x => x.Id);
            query = query.Where(x => x.Allocations.Any(a => periodIds.Contains(a.PeriodId)));
        }
        if (!string.IsNullOrWhiteSpace(request.PayerSearch))
        {
            var value = request.PayerSearch.Trim();
            var normalizedPhone = Person.NormalizePhone(value);
            var personIds = string.IsNullOrEmpty(normalizedPhone)
                ? db.People.Where(x => x.FullName.Contains(value)).Select(x => x.Id)
                : db.People.Where(x => x.FullName.Contains(value)
                    || x.NormalizedPhoneNumber.Contains(normalizedPhone)).Select(x => x.Id);
            query = query.Where(x => personIds.Contains(x.PersonId));
        }
        var page = Math.Max(1, request.Page); var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var payments = await query.OrderByDescending(x => x.PaidAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Include(x => x.Allocations).ToListAsync(cancellationToken);
        var paymentIds = payments.Select(x => x.Id).ToArray();
        var allowedIds = await accessible.ToArrayAsync(cancellationToken);
        var credits = await db.AssociationCredits.AsNoTracking().Where(x => paymentIds.Contains(x.PaymentId)
            && allowedIds.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var personIdsForPage = payments.Select(x => x.PersonId).Distinct().ToArray();
        var payerNames = await db.People.AsNoTracking().Where(x => personIdsForPage.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FullName, cancellationToken);
        var associationNames = await db.Associations.AsNoTracking().Where(x => allowedIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var periodIdsForPage = payments.SelectMany(x => x.Allocations).Select(x => x.PeriodId).Distinct().ToArray();
        var periodNumbers = await db.Periods.AsNoTracking().Where(x => periodIdsForPage.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PeriodNumber, cancellationToken);
        var items = payments.Select(payment =>
        {
            var allocations = payment.Allocations.Where(x => allowedIds.Contains(x.AssociationId))
                .Select(x => new PaymentListAllocationDto(x.AssociationId,
                    associationNames.GetValueOrDefault(x.AssociationId, string.Empty), x.PeriodId,
                    periodNumbers.GetValueOrDefault(x.PeriodId), x.AllocatedAmount, x.DueDate, x.LateDays)).ToList();
            var paymentCredits = credits.Where(x => x.PaymentId == payment.Id)
                .Select(x => new PaymentCreditDto(x.AssociationId, x.Amount)).ToList();
            return new PaymentListItemDto(payment.Id, payment.PersonId,
                payerNames.GetValueOrDefault(payment.PersonId, string.Empty), payment.RecordedByUserId,
                allocations.Sum(x => x.Amount) + paymentCredits.Sum(x => x.Amount), payment.Method,
                payment.PaidAtUtc, payment.EntryType, payment.AdjustsPaymentId, allocations, paymentCredits);
        }).ToList();
        return new PagedResult<PaymentListItemDto>(items, page, pageSize, total);
    }
}

public sealed record GetPaymentPageQuery(Guid PersonId) : IRequest<PaymentPageDto>;
public sealed class GetPaymentPageHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access, IClock clock)
    : IRequestHandler<GetPaymentPageQuery, PaymentPageDto>
{
    public async Task<PaymentPageDto> Handle(GetPaymentPageQuery request, CancellationToken cancellationToken)
    {
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? throw new ForbiddenException(),
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var membershipData = await db.Memberships.AsNoTracking()
            .Where(x => x.PersonId == request.PersonId && x.Kind == MembershipKind.Paying && accessible.Contains(x.AssociationId))
            .Join(db.Associations.AsNoTracking().Where(a => a.Status == AssociationStatus.Active || a.Status == AssociationStatus.Paused),
                m => m.AssociationId, a => a.Id, (m, a) => new { m, a }).ToListAsync(cancellationToken);
        if (membershipData.Count == 0)
            throw new NotFoundException("person.not_found", "Person was not found.");
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PersonId && x.IsActive, cancellationToken)
            ?? throw new NotFoundException("person.not_found", "Person was not found.");
        var ids = membershipData.Select(x => x.a.Id).Distinct().ToArray();
        var allocations = await db.PaymentAllocations.AsNoTracking().Where(x => ids.Contains(x.AssociationId))
            .Join(db.Payments.AsNoTracking().Where(x => x.PersonId == person.Id),
                allocation => allocation.PaymentId, payment => payment.Id, (allocation, _) => allocation)
            .ToListAsync(cancellationToken);
        var credits = await db.AssociationCredits.AsNoTracking().Where(x => x.PersonId == person.Id && ids.Contains(x.AssociationId))
            .GroupBy(x => x.AssociationId).Select(x => new { Id = x.Key, Amount = x.Sum(c => c.Amount) }).ToDictionaryAsync(x => x.Id, x => x.Amount, cancellationToken);
        var creditApplications = await db.AssociationCreditApplications.AsNoTracking()
            .Where(x => x.PersonId == person.Id && ids.Contains(x.AssociationId))
            .ToListAsync(cancellationToken);
        var today = clock.Today;
        var periods = await db.Periods.AsNoTracking().Where(x => ids.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var result = new List<AccessibleAssociationDueDto>();
        var dueItems = new List<PaymentDueItemDto>();
        var previousOverdue = 0m;
        var currentDue = 0m;
        foreach (var group in membershipData.GroupBy(x => x.a.Id))
        {
            var association = group.First().a;
            var requirements = new Dictionary<Guid, decimal>();
            foreach (var item in group)
            {
                var joined = DateOnly.FromDateTime(item.m.JoinedAtUtc.UtcDateTime);
                var left = item.m.LeftAtUtc.HasValue
                    ? DateOnly.FromDateTime(item.m.LeftAtUtc.Value.UtcDateTime)
                    : (DateOnly?)null;
                foreach (var period in periods.Where(x => x.AssociationId == association.Id && x.PeriodDate >= joined
                             && (!left.HasValue || x.PeriodDate <= left.Value)))
                    requirements[period.Id] = requirements.GetValueOrDefault(period.Id)
                        + association.MonthlyContribution * item.m.ShareCount;
            }
            var totalCredit = credits.GetValueOrDefault(association.Id);
            var appliedCredit = creditApplications.Where(x => x.AssociationId == association.Id).Sum(x => x.Amount);
            var availableCredit = totalCredit - appliedCredit;
            var displayedCredit = availableCredit;
            var required = requirements.Sum(x => x.Value);
            var paid = allocations.Where(x => x.AssociationId == association.Id).Sum(x => x.AllocatedAmount);
            var remaining = Math.Max(0, required - paid - totalCredit);
            var dueNow = 0m;
            DateOnly? oldest = null;
            var currentPeriodId = periods.Where(x => x.AssociationId == association.Id && x.PeriodDate <= today)
                .OrderBy(x => x.PeriodDate).Select(x => (Guid?)x.Id).LastOrDefault();
            foreach (var period in periods.Where(x => x.AssociationId == association.Id).OrderBy(x => x.DueDate))
            {
                var periodPaid = allocations.Where(x => x.AssociationId == association.Id && x.PeriodId == period.Id)
                    .Sum(x => x.AllocatedAmount)
                    + creditApplications.Where(x => x.AssociationId == association.Id && x.PeriodId == period.Id)
                        .Sum(x => x.Amount);
                var periodRemaining = Math.Max(0, requirements.GetValueOrDefault(period.Id) - periodPaid);
                var simulatedCredit = Math.Min(periodRemaining, availableCredit);
                periodRemaining -= simulatedCredit;
                availableCredit -= simulatedCredit;
                if (periodRemaining > 0 && period.PeriodDate <= today)
                {
                    dueNow += periodRemaining;
                    dueItems.Add(new PaymentDueItemDto(association.Id, association.Name, period.Id,
                        period.PeriodNumber, period.DueDate, periodRemaining));
                    if (period.Id == currentPeriodId) currentDue += periodRemaining;
                    else previousOverdue += periodRemaining;
                    oldest ??= period.DueDate;
                }
            }
            result.Add(new AccessibleAssociationDueDto(association.Id, association.Name, association.MonthlyContribution,
                dueNow, remaining, oldest, displayedCredit,
                remaining == 0 ? "Paid" : dueNow > 0 ? "Outstanding" : "Upcoming"));
        }
        var orderedDueItems = dueItems.OrderBy(x => x.DueDate).ThenBy(x => x.AssociationId)
            .ThenBy(x => x.PeriodId).ToArray();
        return new PaymentPageDto(person.Id, person.FullName, person.SubscriberCode.Value, result, orderedDueItems,
            previousOverdue, currentDue, result.Sum(x => x.AmountDueNow), result.Sum(x => x.TotalRemaining));
    }
}
