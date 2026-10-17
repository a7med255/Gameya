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
    int? AdvanceYear = null, int? AdvanceMonth = null, decimal? AdvanceAmount = null,
    string? PreviewToken = null, string? IdempotencyKey = null) : IRequest<PaymentDto>;
public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithErrorCode("person.required");
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, false).WithErrorCode("payment.invalid_amount");
        RuleFor(x => x.Method).IsInEnum().WithErrorCode("payment.invalid_method");
        RuleFor(x => x.IdempotencyKey).MaximumLength(100).WithErrorCode("payment.invalid_idempotency_key");
        RuleFor(x => x.AdvanceMonth).InclusiveBetween(1, 12).When(x => x.AdvanceMonth.HasValue)
            .WithErrorCode("payment.invalid_advance_month");
        RuleFor(x => x.AdvanceAmount).GreaterThan(0).When(x => x.AdvanceAmount.HasValue)
            .WithErrorCode("payment.invalid_advance_amount");
    }
}

public sealed class RecordPaymentHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    PaymentAllocationService allocationService,
    IClock clock,
    IAuditService audit) : IRequestHandler<RecordPaymentCommand, PaymentDto>
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

            var plan = await allocationService.BuildPlanAsync(new PaymentPlanningRequest(
                request.PersonId, request.Amount, request.AdvanceYear, request.AdvanceMonth,
                request.AdvanceAmount, request.AssociationIds, request.CreditAssociationId), innerCt);

            if (!string.IsNullOrWhiteSpace(request.PreviewToken) && plan.Preview.PreviewToken != request.PreviewToken)
                throw new ConflictException("payment.preview_stale", "تغيرت حالة الاستحقاقات أثناء تأكيد الدفعة، يرجى مراجعة المعاينة مرة أخرى.");

            var payment = new Payment(request.PersonId, recorderId, request.Amount, request.Method, now, now,
                idempotencyKey: request.IdempotencyKey);

            foreach (var item in plan.Allocations)
            {
                payment.Allocate(item.AssociationId, item.PeriodId, item.Amount, item.DueDate, clock.Today);
            }

            await db.AddEntityAsync(payment, innerCt);

            foreach (var item in plan.CreditApplications)
            {
                await db.AddEntityAsync(new AssociationCreditApplication(request.PersonId, item.AssociationId,
                    item.PeriodId, item.Amount, now), innerCt);
            }

            if (plan.Preview.CreditAmount > 0)
            {
                await db.AddEntityAsync(new AssociationCredit(payment.Id, request.PersonId, plan.CreditAssociationId,
                    plan.Preview.CreditAmount, now), innerCt);
            }

            await db.SaveChangesAsync(innerCt);
            await audit.WriteAsync("PaymentRecorded", nameof(Payment), payment.Id.ToString(), null,
                new { request.PersonId, request.Amount, request.Method, Allocations = payment.Allocations.Count, Credit = plan.Preview.CreditAmount }, innerCt);

            return new PaymentDto(payment.Id, payment.PersonId, payment.Amount, payment.Method, payment.PaidAtUtc,
                payment.EntryType, payment.Allocations.Select(x => new PaymentAllocationDto(x.AssociationId, x.PeriodId,
                    x.AllocatedAmount, x.DueDate, x.LateDays)).ToList(), plan.Preview.CreditAmount > 0
                    ? [new PaymentCreditDto(plan.CreditAssociationId, plan.Preview.CreditAmount)] : []);
        }, cancellationToken);
}

public sealed record PreviewPaymentQuery(Guid PersonId, decimal Amount, int? AdvanceYear = null,
    int? AdvanceMonth = null, decimal? AdvanceAmount = null,
    IReadOnlyCollection<Guid>? AssociationIds = null, Guid? CreditAssociationId = null) : IRequest<PaymentPreviewDto>;

public sealed class PreviewPaymentValidator : AbstractValidator<PreviewPaymentQuery>
{
    public PreviewPaymentValidator()
    {
        RuleFor(x => x.PersonId).NotEmpty().WithErrorCode("person.required");
        RuleFor(x => x.Amount).GreaterThan(0).PrecisionScale(18, 2, false).WithErrorCode("payment.invalid_amount");
        RuleFor(x => x.AdvanceMonth).InclusiveBetween(1, 12).When(x => x.AdvanceMonth.HasValue)
            .WithErrorCode("payment.invalid_advance_month");
        RuleFor(x => x.AdvanceAmount).GreaterThan(0).When(x => x.AdvanceAmount.HasValue)
            .WithErrorCode("payment.invalid_advance_amount");
    }
}

public sealed class PreviewPaymentHandler(PaymentAllocationService allocationService)
    : IRequestHandler<PreviewPaymentQuery, PaymentPreviewDto>
{
    public async Task<PaymentPreviewDto> Handle(PreviewPaymentQuery request, CancellationToken cancellationToken)
    {
        var plan = await allocationService.BuildPlanAsync(new PaymentPlanningRequest(
            request.PersonId, request.Amount, request.AdvanceYear, request.AdvanceMonth,
            request.AdvanceAmount, request.AssociationIds, request.CreditAssociationId), cancellationToken);
        return plan.Preview;
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
public sealed class GetPaymentPageHandler(PaymentAllocationService allocationService)
    : IRequestHandler<GetPaymentPageQuery, PaymentPageDto>
{
    public Task<PaymentPageDto> Handle(GetPaymentPageQuery request, CancellationToken cancellationToken) =>
        allocationService.GetPageAsync(request.PersonId, cancellationToken);
}
