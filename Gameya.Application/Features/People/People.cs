using FluentValidation;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Entities;
using Gameya.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.People;

public sealed record CreatePersonCommand(string FullName, string PhoneNumber) : IRequest<PersonDto>;
public sealed class CreatePersonValidator : AbstractValidator<CreatePersonCommand>
{
    public CreatePersonValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200).WithErrorCode("person.name_required");
        RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30).Matches("^[0-9+ ]+$").WithErrorCode("person.invalid_phone");
    }
}
public sealed class CreatePersonHandler(IApplicationDbContext db, ISubscriberCodeGenerator codes, IClock clock, IAuditService audit)
    : IRequestHandler<CreatePersonCommand, PersonDto>
{
    public async Task<PersonDto> Handle(CreatePersonCommand request, CancellationToken cancellationToken)
    {
        var phone = request.PhoneNumber.Trim();
        var normalizedName = Person.NormalizeName(request.FullName);
        if (await db.People.AnyAsync(x => x.PhoneNumber == phone, cancellationToken))
            throw new ConflictException("person.duplicate_phone", "Phone number already exists.");
        // Exact normalized-name uniqueness is isolated here so the rule can be replaced when the owner finalizes it.
        if (await db.People.AnyAsync(x => x.NormalizedFullName == normalizedName, cancellationToken))
            throw new ConflictException("person.duplicate_name", "A person with the same normalized name already exists.");

        SubscriberCode code;
        do code = SubscriberCode.Create(codes.Generate());
        while (await db.People.AnyAsync(x => x.SubscriberCode == code, cancellationToken));

        var person = new Person(request.FullName, phone, code, clock.UtcNow);
        await db.AddEntityAsync(person, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync("PersonCreated", nameof(Person), person.Id.ToString(), null,
            new { person.FullName, person.PhoneNumber, SubscriberCode = person.SubscriberCode.Value }, cancellationToken);
        return new PersonDto(person.Id, person.FullName, person.PhoneNumber, person.SubscriberCode.Value, person.IsActive, person.CreatedAtUtc);
    }
}

public sealed record GetPeopleQuery(string? Search, int Page = 1, int PageSize = 20) : IRequest<PagedResult<PersonDto>>;
public sealed class GetPeopleHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access)
    : IRequestHandler<GetPeopleQuery, PagedResult<PersonDto>>
{
    public async Task<PagedResult<PersonDto>> Handle(GetPeopleQuery request, CancellationToken cancellationToken)
    {
        var query = db.People.AsNoTracking();
        if (current.IsInRole(AppRoles.PaymentUser))
        {
            var ids = access.GetAccessibleAssociationIds(current.UserId!, false, false);
            var personIds = db.Memberships.Where(x => ids.Contains(x.AssociationId)).Select(x => x.PersonId).Distinct();
            query = query.Where(x => personIds.Contains(x.Id));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            SubscriberCode? subscriberCode = null;
            var candidate = search.ToUpperInvariant();
            if (candidate.Length == SubscriberCode.Length && candidate.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'))
                subscriberCode = SubscriberCode.Create(candidate);
            query = query.Where(x => x.FullName.Contains(search) || subscriberCode != null && x.SubscriberCode == subscriberCode);
        }
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var people = await query.OrderBy(x => x.FullName).Skip((page - 1) * size).Take(size).ToListAsync(cancellationToken);
        var items = people.Select(x => new PersonDto(x.Id, x.FullName, x.PhoneNumber, x.SubscriberCode.Value,
            x.IsActive, x.CreatedAtUtc)).ToList();
        return new PagedResult<PersonDto>(items, page, size, total);
    }
}

public sealed record GetPersonProfileQuery(Guid PersonId, bool ParticipantView = false) : IRequest<PersonProfileDto>;
public sealed class GetPersonProfileHandler(IApplicationDbContext db, ICurrentUser current, IAssociationAccessService access)
    : IRequestHandler<GetPersonProfileQuery, PersonProfileDto>
{
    public async Task<PersonProfileDto> Handle(GetPersonProfileQuery request, CancellationToken cancellationToken)
    {
        if (request.ParticipantView && current.PersonId != request.PersonId) throw new NotFoundException("person.not_found", "Person was not found.");
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PersonId, cancellationToken)
            ?? throw new NotFoundException("person.not_found", "Person was not found.");
        var accessible = access.GetAccessibleAssociationIds(current.UserId ?? string.Empty,
            current.IsInRole(AppRoles.Admin), current.IsInRole(AppRoles.Owner));
        var membershipsQuery = db.Memberships.AsNoTracking().Where(x => x.PersonId == person.Id);
        if (!request.ParticipantView && !current.IsInRole(AppRoles.Admin)) membershipsQuery = membershipsQuery.Where(x => accessible.Contains(x.AssociationId));
        var memberships = await membershipsQuery.Join(db.Associations.AsNoTracking(), m => m.AssociationId, a => a.Id, (m, a) => new { m, a }).ToListAsync(cancellationToken);
        var associationIds = memberships.Select(x => x.a.Id).ToArray();
        var membershipIds = memberships.Select(x => x.m.Id).ToArray();
        var payoutEntities = await db.Payouts.AsNoTracking().Where(x => associationIds.Contains(x.AssociationId)
            && (x.MembershipId.HasValue && membershipIds.Contains(x.MembershipId.Value) || x.BeneficiaryPersonId == person.Id)).ToListAsync(cancellationToken);
        var periodIds = payoutEntities.Select(x => x.PeriodId).Distinct().ToArray();
        var periodNumbers = await db.Periods.AsNoTracking().Where(x => periodIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.PeriodNumber, cancellationToken);
        var associationById = memberships.Select(x => x.a).DistinctBy(x => x.Id).ToDictionary(x => x.Id);
        var payouts = payoutEntities.Select(x => new PayoutDto(x.Id, x.PeriodId, periodNumbers[x.PeriodId], x.RecipientType,
            x.MembershipId, x.BeneficiaryPersonId, x.ShareCount,
            x.ShareCount * associationById[x.AssociationId].FullSharePayoutValue, x.Status)).ToList();
        var creditRows = await db.AssociationCredits.AsNoTracking().Where(x => x.PersonId == person.Id
            && associationIds.Contains(x.AssociationId)).ToListAsync(cancellationToken);
        var creditPaymentIds = creditRows.Select(x => x.PaymentId).ToArray();
        var payments = await db.Payments.AsNoTracking().Where(x => x.PersonId == person.Id
                && (x.Allocations.Any(a => associationIds.Contains(a.AssociationId)) || creditPaymentIds.Contains(x.Id)))
            .Include(x => x.Allocations)
            .ToListAsync(cancellationToken);
        var paymentDtos = payments.Select(x =>
        {
            var allocations = x.Allocations.Where(a => associationIds.Contains(a.AssociationId))
                .Select(a => new PaymentAllocationDto(a.AssociationId, a.PeriodId, a.AllocatedAmount, a.DueDate, a.LateDays)).ToList();
            var credits = creditRows.Where(c => c.PaymentId == x.Id)
                .Select(c => new PaymentCreditDto(c.AssociationId, c.Amount)).ToList();
            return new PaymentDto(x.Id, x.PersonId, allocations.Sum(a => a.Amount) + credits.Sum(c => c.Amount),
                x.Method, x.PaidAtUtc, x.EntryType, allocations, credits);
        }).ToList();
        var paidByAssociation = paymentDtos.SelectMany(x => x.Allocations.Select(a => (a.AssociationId, a.Amount))
                .Concat(x.Credits.Select(c => (c.AssociationId, c.Amount))))
            .GroupBy(x => x.AssociationId).ToDictionary(x => x.Key, x => x.Sum(a => a.Amount));
        var membershipDtos = memberships.Select(x =>
        {
            var required = x.m.Kind == Domain.Enums.MembershipKind.Paying ? x.a.FullSharePayoutValue * x.m.ShareCount : 0;
            var paid = paidByAssociation.GetValueOrDefault(x.a.Id);
            return new PersonMembershipDto(x.m.Id, x.a.Id, x.a.Name, x.m.Status, x.m.Kind, x.m.ShareCount, paid, Math.Max(0, required - paid));
        }).ToList();
        var settlements = await db.Settlements.AsNoTracking().Where(x => membershipIds.Contains(x.MembershipId))
            .Select(x => new SettlementDto(x.Id, x.AssociationId, x.MembershipId, x.TotalRequired, x.TotalPaid, x.Balance, x.Status)).ToListAsync(cancellationToken);
        var obligations = membershipDtos.Sum(x => x.Paid + x.Remaining);
        var paidTotal = membershipDtos.Sum(x => x.Paid);
        return new PersonProfileDto(new PersonDto(person.Id, person.FullName, person.PhoneNumber, person.SubscriberCode.Value,
            person.IsActive, person.CreatedAtUtc), membershipDtos, payouts, paymentDtos, settlements, obligations, paidTotal, Math.Max(0, obligations - paidTotal));
    }
}
