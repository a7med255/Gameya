using FluentValidation;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Associations;

public sealed record CreateAssociationCommand(string Name, DateOnly StartDate, int NumberOfMonths, decimal MonthlyContribution)
    : IRequest<AssociationDetailsDto>;
public sealed class CreateAssociationValidator : AbstractValidator<CreateAssociationCommand>
{
    public CreateAssociationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("association.name_required");
        RuleFor(x => x.NumberOfMonths).InclusiveBetween(1, 120).WithErrorCode("association.invalid_months");
        RuleFor(x => x.MonthlyContribution).GreaterThan(0).PrecisionScale(18, 2, false).WithErrorCode("association.invalid_contribution");
    }
}
public sealed class CreateAssociationHandler(IApplicationDbContext db, ICurrentUser current, IAssociationCalendar calendar,
    IClock clock, IAuditService audit) : IRequestHandler<CreateAssociationCommand, AssociationDetailsDto>
{
    public async Task<AssociationDetailsDto> Handle(CreateAssociationCommand request, CancellationToken cancellationToken)
    {
        var ownerId = current.UserId ?? throw new ForbiddenException();
        var normalizedName = Association.NormalizeName(request.Name);
        if (await db.Associations.AnyAsync(x => x.OwnerId == ownerId && x.NormalizedName == normalizedName,
                cancellationToken))
            throw new ConflictException("association.duplicate_name", "An association with this name already exists.");
        var association = new Association(request.Name, request.StartDate, request.NumberOfMonths,
            request.MonthlyContribution, ownerId, clock.UtcNow);
        association.SetPeriods(calendar.CreatePeriods(request.StartDate, request.NumberOfMonths));
        await db.AddEntityAsync(association, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AssociationCreated", nameof(Association), association.Id.ToString(), null,
            new { association.Name, association.StartDate, association.NumberOfMonths, association.MonthlyContribution }, cancellationToken);
        return AssociationMappings.ToDetails(association, new Dictionary<Guid, string>());
    }
}

public sealed record GetAssociationsQuery(string? Search, AssociationStatus? Status, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<AssociationSummaryDto>>;
public sealed class GetAssociationsHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access)
    : IRequestHandler<GetAssociationsQuery, PagedResult<AssociationSummaryDto>>
{
    public async Task<PagedResult<AssociationSummaryDto>> Handle(GetAssociationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.Associations.AsNoTracking();
        if (!current.IsInRole(AppRoles.Admin))
        {
            var ids = access.GetAccessibleAssociationIds(current.UserId ?? string.Empty, false, current.IsInRole(AppRoles.Owner));
            query = query.Where(x => ids.Contains(x.Id));
        }
        if (!string.IsNullOrWhiteSpace(request.Search)) query = query.Where(x => x.Name.Contains(request.Search.Trim()));
        if (request.Status.HasValue) query = query.Where(x => x.Status == request.Status.Value);
        var page = Math.Max(1, request.Page); var size = Math.Clamp(request.PageSize, 1, 100);
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * size).Take(size)
            .Select(x => new AssociationSummaryDto(x.Id, x.Name, x.StartDate, x.EndDate, x.NumberOfMonths,
                x.MonthlyContribution, x.Status, x.Memberships.Count)).ToListAsync(cancellationToken);
        return new PagedResult<AssociationSummaryDto>(items, page, size, count);
    }
}

public sealed record GetAssociationByIdQuery(Guid AssociationId) : IRequest<AssociationDetailsDto>;
public sealed class GetAssociationByIdHandler(IApplicationDbContext db, IAssociationAccessService access)
    : IRequestHandler<GetAssociationByIdQuery, AssociationDetailsDto>
{
    public async Task<AssociationDetailsDto> Handle(GetAssociationByIdQuery request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var result = await db.Associations.AsNoTracking().Where(x => x.Id == request.AssociationId)
            .Select(x => new AssociationDetailsDto(x.Id, x.Name, x.StartDate, x.EndDate, x.NumberOfMonths,
                x.MonthlyContribution, x.Status, x.OwnerId,
                x.Memberships.OrderBy(m => m.DisplayOrder).Select(m => new MembershipDto(m.Id, m.PersonId,
                    db.People.Where(p => p.Id == m.PersonId).Select(p => p.FullName).First(), m.ShareCount, m.Kind,
                    m.Status, m.DisplayOrder, m.JoinedAtUtc, m.LeftAtUtc)).ToList(),
                x.Periods.OrderBy(p => p.PeriodNumber).Select(p => new PeriodDto(p.Id, p.PeriodNumber, p.PeriodDate, p.DueDate, p.Status)).ToList(),
                x.Payouts.Select(p => new PayoutDto(p.Id, p.PeriodId,
                    db.Periods.Where(period => period.Id == p.PeriodId).Select(period => period.PeriodNumber).First(),
                    p.RecipientType, p.MembershipId, p.BeneficiaryPersonId, p.ShareCount,
                    p.ShareCount * x.FullSharePayoutValue, p.Status)).ToList()))
            .SingleOrDefaultAsync(cancellationToken);
        return result ?? throw new NotFoundException("association.not_found", "Association was not found.");
    }
}

public sealed record AddMemberCommand(Guid AssociationId, Guid PersonId, decimal ShareCount, MembershipKind Kind) : IRequest<Guid>;
public sealed class AddMemberValidator : AbstractValidator<AddMemberCommand>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.AssociationId).NotEmpty().WithErrorCode("association.not_found");
        RuleFor(x => x.PersonId).NotEmpty().WithErrorCode("person.required");
        RuleFor(x => x.Kind).IsInEnum().WithErrorCode("membership.invalid_kind");
        RuleFor(x => x.Kind).Equal(MembershipKind.Paying).WithErrorCode("membership.advance_is_virtual");
        RuleFor(x => x.ShareCount).GreaterThan(0).Must(x => x * 2 == decimal.Truncate(x * 2)).WithErrorCode("membership.invalid_shares");
    }
}
public sealed class AddMemberHandler(IApplicationDbContext db, IAssociationAccessService access,
    IPersonAccessService personAccess, IClock clock, IAuditService audit)
    : IRequestHandler<AddMemberCommand, Guid>
{
    public async Task<Guid> Handle(AddMemberCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        await personAccess.EnsureAccessAsync(request.PersonId, cancellationToken);
        if (!await db.People.AnyAsync(x => x.Id == request.PersonId && x.IsActive, cancellationToken))
            throw new NotFoundException("person.not_found", "Person was not found.");
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var member = association.AddMember(request.PersonId, request.ShareCount, request.Kind, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MembershipAdded", nameof(AssociationMembership), member.Id.ToString(), null,
            new { request.AssociationId, request.PersonId, request.ShareCount, request.Kind }, cancellationToken);
        return member.Id;
    }
}

public sealed record ChangeMemberSharesCommand(Guid AssociationId, Guid MembershipId, decimal ShareCount) : IRequest;
public sealed class ChangeMemberSharesValidator : AbstractValidator<ChangeMemberSharesCommand>
{
    public ChangeMemberSharesValidator() => RuleFor(x => x.ShareCount).GreaterThan(0)
        .Must(x => x * 2 == decimal.Truncate(x * 2)).WithErrorCode("membership.invalid_shares");
}
public sealed class ChangeMemberSharesHandler(IApplicationDbContext db, IAssociationAccessService access,
    IClock clock, IAuditService audit) : IRequestHandler<ChangeMemberSharesCommand>
{
    public async Task Handle(ChangeMemberSharesCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var oldShares = association.Memberships.SingleOrDefault(x => x.Id == request.MembershipId)?.ShareCount
            ?? throw new NotFoundException("membership.not_found", "Membership was not found.");
        association.ChangeMemberShares(request.MembershipId, request.ShareCount, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MembershipSharesChanged", nameof(AssociationMembership), request.MembershipId.ToString(),
            new { ShareCount = oldShares }, new { request.ShareCount }, cancellationToken);
    }
}

public sealed record RemoveMemberCommand(Guid AssociationId, Guid MembershipId) : IRequest;
public sealed class RemoveMemberHandler(IApplicationDbContext db, IAssociationAccessService access,
    IClock clock, IAuditService audit) : IRequestHandler<RemoveMemberCommand>
{
    public async Task Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.RemoveDraftMember(request.MembershipId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MembershipRemoved", nameof(AssociationMembership), request.MembershipId.ToString(),
            new { request.AssociationId }, null, cancellationToken);
    }
}

public sealed record StartAssociationCommand(Guid AssociationId) : IRequest;
public sealed class StartAssociationHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<StartAssociationCommand>
{
    public async Task Handle(StartAssociationCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.Start(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AssociationStarted", nameof(Association), association.Id.ToString(), new { Status = "Draft" }, new { Status = "Active" }, cancellationToken);
    }
}
public sealed record PauseAssociationCommand(Guid AssociationId) : IRequest;
public sealed class PauseAssociationHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<PauseAssociationCommand>
{
    public async Task Handle(PauseAssociationCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.Pause(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AssociationPaused", nameof(Association), association.Id.ToString(), new { Status = "Active" }, new { Status = "Paused" }, cancellationToken);
    }
}
public sealed record CancelAssociationCommand(Guid AssociationId) : IRequest;
public sealed class CancelAssociationHandler(IApplicationDbContext db, IAssociationAccessService access,
    IClock clock, IAuditService audit) : IRequestHandler<CancelAssociationCommand>
{
    public async Task Handle(CancelAssociationCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var previous = association.Status;
        association.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AssociationCancelled", nameof(Association), association.Id.ToString(),
            new { Status = previous }, new { Status = AssociationStatus.Cancelled }, cancellationToken);
    }
}
public sealed record BulkCancelAssociationsCommand(IReadOnlyCollection<Guid> AssociationIds) : IRequest;
public sealed class BulkCancelAssociationsValidator : AbstractValidator<BulkCancelAssociationsCommand>
{
    public BulkCancelAssociationsValidator() => RuleFor(x => x.AssociationIds).NotEmpty()
        .Must(ids => ids.Distinct().Count() == ids.Count).WithErrorCode("association.invalid_selection");
}
public sealed class BulkCancelAssociationsHandler(IApplicationDbContext db, IAssociationAccessService access,
    IClock clock, IAuditService audit) : IRequestHandler<BulkCancelAssociationsCommand>
{
    public async Task Handle(BulkCancelAssociationsCommand request, CancellationToken cancellationToken)
    {
        var ids = request.AssociationIds.Distinct().ToArray();
        foreach (var id in ids) await access.EnsureAccessAsync(id, cancellationToken);
        var associations = new List<Association>();
        foreach (var id in ids)
            associations.Add(await db.FindAssociationForUpdateAsync(id, cancellationToken)
                ?? throw new NotFoundException("association.not_found", "Association was not found."));
        if (associations.Any(x => x.Status is AssociationStatus.Completed or AssociationStatus.Cancelled))
            throw new ConflictException("association.cancel_state",
                "Completed or already cancelled associations cannot be cancelled.");
        foreach (var association in associations) association.Cancel(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AssociationsBulkCancelled", nameof(Association), string.Join(',', ids), null,
            new { AssociationIds = ids, Status = AssociationStatus.Cancelled }, cancellationToken);
    }
}
public sealed record ResumeAssociationCommand(Guid AssociationId) : IRequest;
public sealed class ResumeAssociationHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<ResumeAssociationCommand>
{
    public async Task Handle(ResumeAssociationCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.Resume(clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("AssociationResumed", nameof(Association), association.Id.ToString(),
            new { Status = "Paused" }, new { Status = "Active" }, cancellationToken);
    }
}
public sealed record ClosePeriodCommand(Guid AssociationId, Guid PeriodId) : IRequest;
public sealed class ClosePeriodHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<ClosePeriodCommand>
{
    public async Task Handle(ClosePeriodCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.ClosePeriod(request.PeriodId, clock.UtcNow); await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PeriodClosed", nameof(AssociationPeriod), request.PeriodId.ToString(), null, new { Status = "Closed" }, cancellationToken);
    }
}
public sealed record CompleteAssociationCommand(Guid AssociationId) : IRequest;
public sealed class CompleteAssociationHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<CompleteAssociationCommand>
{
    public async Task Handle(CompleteAssociationCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        await db.ExecuteSerializableAsync(async innerCancellationToken =>
        {
            var association = await db.FindAssociationForUpdateAsync(request.AssociationId, innerCancellationToken)
                ?? throw new NotFoundException("association.not_found", "Association was not found.");
            var now = clock.UtcNow;
            association.Complete(now);
            await AssociationSettlementGenerator.CreateForReplacedMembersAsync(db, association, now,
                innerCancellationToken);
            await db.SaveChangesAsync(innerCancellationToken);
            return true;
        }, cancellationToken);
        await audit.WriteAsync("AssociationCompleted", nameof(Association), request.AssociationId.ToString(), null, new { Status = "Completed" }, cancellationToken);
    }
}

public sealed record AddPayoutCommand(Guid AssociationId, Guid PeriodId, PayoutRecipientType RecipientType,
    Guid? MembershipId, decimal ShareCount) : IRequest<Guid>;
public sealed class AddPayoutValidator : AbstractValidator<AddPayoutCommand>
{
    public AddPayoutValidator()
    {
        RuleFor(x => x.ShareCount).GreaterThan(0).Must(x => x * 2 == decimal.Truncate(x * 2))
            .WithErrorCode("payout.invalid_shares");
        RuleFor(x => x.RecipientType).IsInEnum().WithErrorCode("payout.invalid_recipient_type");
        RuleFor(x => x.MembershipId).NotNull().When(x => x.RecipientType == PayoutRecipientType.Membership).WithErrorCode("payout.membership_required");
        RuleFor(x => x.ShareCount).Equal(1)
            .When(x => x.RecipientType is PayoutRecipientType.ReserveSlot or PayoutRecipientType.AdvanceSlot)
            .WithErrorCode("payout.virtual_full_share");
    }
}
public sealed class AddPayoutHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<AddPayoutCommand, Guid>
{
    public async Task<Guid> Handle(AddPayoutCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var payout = association.AddPayout(request.PeriodId, request.RecipientType, request.MembershipId,
            request.ShareCount, clock.Today, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PayoutScheduled", nameof(PayoutAssignment), payout.Id.ToString(), null, request, cancellationToken);
        return payout.Id;
    }
}
public sealed record MovePayoutCommand(Guid AssociationId, Guid PayoutId, Guid PeriodId, string? Reason) : IRequest;
public sealed class MovePayoutValidator : AbstractValidator<MovePayoutCommand>
{
    public MovePayoutValidator() => RuleFor(x => x.Reason).MaximumLength(500).WithErrorCode("payout.reason_too_long");
}
public sealed class MovePayoutHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<MovePayoutCommand>
{
    public async Task Handle(MovePayoutCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var oldPeriod = association.Payouts.SingleOrDefault(x => x.Id == request.PayoutId)?.PeriodId;
        association.MoveFuturePayout(request.PayoutId, request.PeriodId, clock.Today, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PayoutScheduleChanged", nameof(PayoutAssignment), request.PayoutId.ToString(),
            new { PeriodId = oldPeriod }, new { request.PeriodId, request.Reason }, cancellationToken);
    }
}

public sealed record RemovePayoutCommand(Guid AssociationId, Guid PayoutId, string? Reason) : IRequest;
public sealed class RemovePayoutValidator : AbstractValidator<RemovePayoutCommand>
{
    public RemovePayoutValidator() => RuleFor(x => x.Reason).MaximumLength(500).WithErrorCode("payout.reason_too_long");
}
public sealed class RemovePayoutHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<RemovePayoutCommand>
{
    public async Task Handle(RemovePayoutCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.RemoveFuturePayout(request.PayoutId, clock.Today, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PayoutScheduleRemoved", nameof(PayoutAssignment), request.PayoutId.ToString(),
            new { request.AssociationId }, new { Removed = true, request.Reason }, cancellationToken);
    }
}

public sealed record CompletePayoutCommand(Guid AssociationId, Guid PayoutId) : IRequest;
public sealed class CompletePayoutHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<CompletePayoutCommand>
{
    public async Task Handle(CompletePayoutCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var automaticallyCompleted = await db.ExecuteSerializableAsync(async innerCancellationToken =>
        {
            var association = await db.FindAssociationForUpdateAsync(request.AssociationId, innerCancellationToken)
                ?? throw new NotFoundException("association.not_found", "Association was not found.");
            var wasCompleted = association.Status == AssociationStatus.Completed;
            var now = clock.UtcNow;
            association.CompletePayout(request.PayoutId, clock.Today, now);
            var completed = !wasCompleted && association.Status == AssociationStatus.Completed;
            if (completed)
                await AssociationSettlementGenerator.CreateForReplacedMembersAsync(db, association, now,
                    innerCancellationToken);
            await db.SaveChangesAsync(innerCancellationToken);
            return completed;
        }, cancellationToken);
        await audit.WriteAsync("PayoutCompleted", nameof(PayoutAssignment), request.PayoutId.ToString(), null,
            new { Status = "Completed" }, cancellationToken);
        if (automaticallyCompleted)
            await audit.WriteAsync("AssociationCompleted", nameof(Association), request.AssociationId.ToString(),
                null, new { Status = "Completed", Trigger = "FinalPayout" }, cancellationToken);
    }
}

public sealed record AssociateReserveBeneficiaryCommand(Guid AssociationId, Guid PayoutId, Guid PersonId) : IRequest;
public sealed class AssociateReserveBeneficiaryHandler(IApplicationDbContext db, IAssociationAccessService access, IClock clock, IAuditService audit)
    : IRequestHandler<AssociateReserveBeneficiaryCommand>
{
    public async Task Handle(AssociateReserveBeneficiaryCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        if (!await db.People.AnyAsync(x => x.Id == request.PersonId, cancellationToken))
            throw new NotFoundException("person.not_found", "Person was not found.");
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        association.AssociateReservePayoutBeneficiary(request.PayoutId, request.PersonId, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("ReserveBeneficiaryAssociated", nameof(PayoutAssignment), request.PayoutId.ToString(), null,
            new { request.PersonId }, cancellationToken);
    }
}

public sealed record ReplacementPersonInput(Guid? PersonId, string? FullName, string? PhoneNumber, decimal ShareCount);
public sealed record ReplaceMemberCommand(Guid AssociationId, Guid MembershipId,
    IReadOnlyCollection<ReplacementPersonInput> Replacements, string? Reason) : IRequest<Guid>;
public sealed class ReplaceMemberValidator : AbstractValidator<ReplaceMemberCommand>
{
    public ReplaceMemberValidator()
    {
        RuleFor(x => x.Replacements).NotEmpty().WithErrorCode("replacement.required");
        RuleFor(x => x.Replacements).Must(x => x is not null && x.Where(y => y.PersonId.HasValue)
                .GroupBy(y => y.PersonId).All(group => group.Count() == 1))
            .WithErrorCode("replacement.duplicate_person");
        RuleFor(x => x.Reason).MaximumLength(500).WithErrorCode("replacement.reason_too_long");
        RuleForEach(x => x.Replacements).ChildRules(x =>
        {
            x.RuleFor(y => y.ShareCount).GreaterThan(0).WithErrorCode("membership.invalid_shares");
            x.RuleFor(y => y.ShareCount).Must(value => value * 2 == decimal.Truncate(value * 2))
                .WithErrorCode("membership.invalid_shares");
            x.RuleFor(y => y.FullName).NotEmpty().MaximumLength(200)
                .Matches(@"^[\p{L}\p{M}][\p{L}\p{M}\p{N} .'-]+$").When(y => y.PersonId is null)
                .WithErrorCode("person.name_required");
            x.RuleFor(y => y.PhoneNumber).NotEmpty().MaximumLength(30)
                .Must(value => value is not null && Person.IsValidEgyptianMobile(value))
                .When(y => y.PersonId is null).WithErrorCode("person.invalid_phone");
        });
    }
}
public sealed class ReplaceMemberHandler(IApplicationDbContext db, IAssociationAccessService access,
    IPersonAccessService personAccess, ICurrentUser current, ISubscriberCodeGenerator codes,
    IClock clock, IAuditService audit) : IRequestHandler<ReplaceMemberCommand, Guid>
{
    public async Task<Guid> Handle(ReplaceMemberCommand request, CancellationToken cancellationToken)
    {
        await access.EnsureAccessAsync(request.AssociationId, cancellationToken);
        var association = await db.FindAssociationForUpdateAsync(request.AssociationId, cancellationToken)
            ?? throw new NotFoundException("association.not_found", "Association was not found.");
        var replacements = new List<(Guid PersonId, decimal ShareCount)>();
        var newPhones = new HashSet<string>(StringComparer.Ordinal);
        var newNames = new HashSet<string>(StringComparer.Ordinal);
        var newCodes = new HashSet<string>(StringComparer.Ordinal);
        var ownerId = current.UserId ?? throw new ForbiddenException();
        var accessiblePersonIds = personAccess.GetAccessiblePersonIds(ownerId,
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        foreach (var input in request.Replacements)
        {
            if (input.PersonId is Guid id)
            {
                await personAccess.EnsureAccessAsync(id, cancellationToken);
                if (!await db.People.AnyAsync(x => x.Id == id && x.IsActive, cancellationToken)) throw new NotFoundException("person.not_found", "Person was not found.");
                replacements.Add((id, input.ShareCount));
                continue;
            }
            var phone = input.PhoneNumber!.Trim();
            var normalizedPhone = Person.NormalizePhone(phone);
            var phoneVariants = Person.PhoneVariants(phone);
            var normalizedName = Person.NormalizeName(input.FullName!);
            if (!newPhones.Add(normalizedPhone) || await db.People.AnyAsync(x => accessiblePersonIds.Contains(x.Id)
                    && phoneVariants.Contains(x.NormalizedPhoneNumber), cancellationToken))
                throw new ConflictException("person.duplicate_phone", "Phone number already exists.");
            if (!newNames.Add(normalizedName) || await db.People.AnyAsync(x => accessiblePersonIds.Contains(x.Id)
                    && x.NormalizedFullName == normalizedName, cancellationToken))
                throw new ConflictException("person.duplicate_name", "A person with the same normalized name already exists.");
            var codeValue = codes.Generate();
            while (!newCodes.Add(codeValue) || await db.People.AnyAsync(
                       x => x.SubscriberCode == Domain.ValueObjects.SubscriberCode.Create(codeValue), cancellationToken))
                codeValue = codes.Generate();
            var person = new Person(input.FullName!, phone, Domain.ValueObjects.SubscriberCode.Create(codeValue), clock.UtcNow);
            await db.AddEntityAsync(person, cancellationToken);
            await personAccess.GrantOwnerAccessAsync(ownerId, person.Id,
                person.NormalizedFullName, person.NormalizedPhoneNumber, cancellationToken);
            replacements.Add((person.Id, input.ShareCount));
        }
        var replacement = association.ReplaceMember(request.MembershipId, replacements, request.Reason, clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("MemberReplaced", nameof(Replacement), replacement.Id.ToString(),
            new { request.MembershipId }, new { Replacements = replacements, request.Reason }, cancellationToken);
        return replacement.Id;
    }
}

internal static class AssociationMappings
{
    public static AssociationDetailsDto ToDetails(Association x, IReadOnlyDictionary<Guid, string> names) =>
        new(x.Id, x.Name, x.StartDate, x.EndDate, x.NumberOfMonths, x.MonthlyContribution, x.Status, x.OwnerId,
            x.Memberships.Select(m => new MembershipDto(m.Id, m.PersonId, names.GetValueOrDefault(m.PersonId, string.Empty),
                m.ShareCount, m.Kind, m.Status, m.DisplayOrder, m.JoinedAtUtc, m.LeftAtUtc))
                .OrderBy(m => m.DisplayOrder).ToList(),
            x.Periods.OrderBy(p => p.PeriodNumber).Select(p => new PeriodDto(p.Id, p.PeriodNumber, p.PeriodDate, p.DueDate, p.Status)).ToList(),
            x.Payouts.Select(p => new PayoutDto(p.Id, p.PeriodId, x.Periods.Single(period => period.Id == p.PeriodId).PeriodNumber,
                p.RecipientType, p.MembershipId, p.BeneficiaryPersonId, p.ShareCount, p.ShareCount * x.FullSharePayoutValue, p.Status)).ToList());
}

internal static class AssociationSettlementGenerator
{
    public static async Task CreateForReplacedMembersAsync(IApplicationDbContext db, Association association,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var membership in association.Memberships.Where(x => x.Status == MembershipStatus.Replaced
                     && x.Kind == MembershipKind.Paying && x.LeftAtUtc.HasValue))
        {
            if (await db.Settlements.AnyAsync(x => x.MembershipId == membership.Id, cancellationToken)) continue;
            var joined = DateOnly.FromDateTime(membership.JoinedAtUtc.UtcDateTime);
            var left = DateOnly.FromDateTime(membership.LeftAtUtc!.Value.UtcDateTime);
            var eligiblePeriodIds = association.Periods.Where(x => x.PeriodDate >= joined && x.PeriodDate <= left)
                .Select(x => x.Id).ToArray();
            var required = eligiblePeriodIds.Length * association.MonthlyContribution * membership.ShareCount;
            var allocated = await db.PaymentAllocations.AsNoTracking()
                .Where(x => x.AssociationId == association.Id && eligiblePeriodIds.Contains(x.PeriodId))
                .Join(db.Payments.AsNoTracking().Where(x => x.PersonId == membership.PersonId),
                    allocation => allocation.PaymentId, payment => payment.Id,
                    (allocation, _) => allocation.AllocatedAmount)
                .SumAsync(cancellationToken);
            var credit = await db.AssociationCredits.AsNoTracking()
                .Where(x => x.AssociationId == association.Id && x.PersonId == membership.PersonId
                    && x.CreatedAtUtc <= membership.LeftAtUtc.Value)
                .SumAsync(x => x.Amount, cancellationToken);
            await db.AddEntityAsync(new Settlement(association.Id, membership.Id, required, allocated + credit, now),
                cancellationToken);
        }
    }
}
