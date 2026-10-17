using FluentValidation;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Domain.Exceptions;
using Gameya.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gameya.Application.Features.Associations;

public sealed record AssociationEditorMemberInput(
    string ClientKey,
    Guid? PersonId,
    string? FullName,
    string? PhoneNumber,
    decimal ShareCount,
    int DisplayOrder);

public sealed record AssociationEditorPayoutInput(
    int PeriodNumber,
    PayoutRecipientType RecipientType,
    string? MemberClientKey,
    decimal ShareCount,
    string? BeneficiaryMemberClientKey);

public sealed record SaveAssociationConfigurationCommand(
    Guid? AssociationId,
    string Name,
    DateOnly StartDate,
    int NumberOfMonths,
    decimal MonthlyContribution,
    IReadOnlyCollection<AssociationEditorMemberInput> Members,
    IReadOnlyCollection<AssociationEditorPayoutInput> Payouts,
    bool StartAssociation) : IRequest<AssociationDetailsDto>;

public sealed class SaveAssociationConfigurationValidator : AbstractValidator<SaveAssociationConfigurationCommand>
{
    public SaveAssociationConfigurationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("association.name_required");
        RuleFor(x => x.NumberOfMonths).InclusiveBetween(1, 120).WithErrorCode("association.invalid_months");
        RuleFor(x => x.MonthlyContribution).GreaterThan(0).PrecisionScale(18, 2, false)
            .WithErrorCode("association.invalid_contribution");
        RuleFor(x => x.Members).NotNull();
        RuleFor(x => x.Payouts).NotNull();
        RuleForEach(x => x.Members).ChildRules(member =>
        {
            member.RuleFor(x => x.ClientKey).NotEmpty().MaximumLength(80);
            member.RuleFor(x => x.ShareCount).GreaterThan(0)
                .Must(value => value * 2 == decimal.Truncate(value * 2))
                .WithErrorCode("membership.invalid_shares");
            member.RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
            member.RuleFor(x => x.FullName).NotEmpty().MaximumLength(200)
                .Matches(@"^[\p{L}\p{M}][\p{L}\p{M}\p{N} .'-]+$").When(x => x.PersonId is null)
                .WithErrorCode("person.name_required");
            member.RuleFor(x => x.PhoneNumber).NotEmpty().MaximumLength(30)
                .Must(value => value is not null && Person.IsValidEgyptianMobile(value))
                .When(x => x.PersonId is null).WithErrorCode("person.invalid_phone");
        });
        RuleForEach(x => x.Payouts).ChildRules(payout =>
        {
            payout.RuleFor(x => x.PeriodNumber).GreaterThan(0);
            payout.RuleFor(x => x.RecipientType).IsInEnum().WithErrorCode("payout.invalid_recipient_type");
            payout.RuleFor(x => x.ShareCount).GreaterThan(0)
                .Must(value => value * 2 == decimal.Truncate(value * 2))
                .WithErrorCode("payout.invalid_shares");
            payout.RuleFor(x => x.MemberClientKey).NotEmpty()
                .When(x => x.RecipientType == PayoutRecipientType.Membership)
                .WithErrorCode("payout.membership_required");
            payout.RuleFor(x => x.ShareCount).Equal(1)
                .When(x => x.RecipientType is PayoutRecipientType.ReserveSlot or PayoutRecipientType.AdvanceSlot)
                .WithErrorCode("payout.virtual_full_share");
        });
        RuleFor(x => x.Members).Must(items => items.Select(item => item.ClientKey)
                .Distinct(StringComparer.Ordinal).Count() == items.Count)
            .WithErrorCode("membership.duplicate_key");
        RuleFor(x => x.Members).Must(items => items.Select(item => item.DisplayOrder).Distinct().Count() == items.Count)
            .WithErrorCode("membership.duplicate_order");
    }
}

public sealed class SaveAssociationConfigurationHandler(
    IApplicationDbContext db,
    ICurrentUser current,
    IAssociationAccessService associationAccess,
    IPersonAccessService personAccess,
    IAssociationCalendar calendar,
    ISubscriberCodeGenerator codes,
    IClock clock,
    IAuditService audit,
    ILogger<SaveAssociationConfigurationHandler> logger)
    : IRequestHandler<SaveAssociationConfigurationCommand, AssociationDetailsDto>
{
    public async Task<AssociationDetailsDto> Handle(
        SaveAssociationConfigurationCommand request, CancellationToken cancellationToken)
    {
        var ownerId = current.UserId ?? throw new ForbiddenException();
        if (request.AssociationId.HasValue)
            await associationAccess.EnsureAccessAsync(request.AssociationId.Value, cancellationToken);
        var previousConfiguration = request.AssociationId.HasValue
            ? await db.Associations.AsNoTracking().Where(x => x.Id == request.AssociationId.Value)
                .Select(x => new AssociationConfigurationAudit(x.Name, x.StartDate, x.NumberOfMonths,
                    x.MonthlyContribution, x.Memberships.Count, x.Payouts.Count, x.Status.ToString()))
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var result = await db.ExecuteSerializableAsync(async innerCancellationToken =>
        {
            Association? existingAssociation = null;
            if (request.AssociationId.HasValue)
                existingAssociation = await db.FindAssociationForUpdateAsync(request.AssociationId.Value,
                        innerCancellationToken)
                    ?? throw new NotFoundException("association.not_found", "Association was not found.");
            var associationOwnerId = existingAssociation?.OwnerId ?? ownerId;
            var normalizedName = Association.NormalizeName(request.Name);
            if (await db.Associations.AnyAsync(x => x.OwnerId == associationOwnerId
                    && x.NormalizedName == normalizedName
                    && (!request.AssociationId.HasValue || x.Id != request.AssociationId.Value),
                    innerCancellationToken))
                throw new ConflictException("association.duplicate_name",
                    "An association with the same normalized name already exists for this owner.");

            Association association;
            if (existingAssociation is not null)
            {
                association = existingAssociation;
                var oldPayouts = association.Payouts.ToArray();
                var oldMemberships = association.Memberships.ToArray();
                var oldPeriods = association.Periods.ToArray();
                db.RemoveEntities(oldPayouts);
                db.RemoveEntities(oldMemberships);
                db.RemoveEntities(oldPeriods);
                association.ResetDraftConfiguration(request.Name, request.StartDate, request.NumberOfMonths,
                    request.MonthlyContribution, calendar.CreatePeriods(request.StartDate, request.NumberOfMonths),
                    clock.UtcNow);
                foreach (var period in association.Periods)
                    await db.AddEntityAsync(period, innerCancellationToken);
            }
            else
            {
                association = new Association(request.Name, request.StartDate, request.NumberOfMonths,
                    request.MonthlyContribution, associationOwnerId, clock.UtcNow);
                association.SetPeriods(calendar.CreatePeriods(request.StartDate, request.NumberOfMonths));
                await db.AddEntityAsync(association, innerCancellationToken);
            }

            var errors = new List<AssociationValidationError>();
            var memberships = new Dictionary<string, AssociationMembership>(StringComparer.Ordinal);
            var people = new Dictionary<string, Person>(StringComparer.Ordinal);
            var ownerPersonIds = personAccess.GetAccessiblePersonIds(associationOwnerId, false, true);
            var knownNames = new HashSet<string>(StringComparer.Ordinal);
            var knownPhones = new HashSet<string>(StringComparer.Ordinal);
            var generatedCodes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var input in request.Members.OrderBy(x => x.DisplayOrder))
            {
                Person? person;
                if (input.PersonId.HasValue)
                {
                    await personAccess.EnsureAccessAsync(input.PersonId.Value, innerCancellationToken);
                    person = await db.People.SingleOrDefaultAsync(x => x.Id == input.PersonId.Value && x.IsActive,
                        innerCancellationToken);
                    if (person is null)
                    {
                        errors.Add(new("person.not_found", $"members[{input.DisplayOrder}].personId", null,
                            "The selected person was not found."));
                        continue;
                    }
                }
                else
                {
                    var normalizedNameInput = Person.NormalizeName(input.FullName!);
                    var normalizedPhoneInput = Person.NormalizePhone(input.PhoneNumber!);
                    var phoneVariants = Person.PhoneVariants(input.PhoneNumber!);
                    var duplicateName = !knownNames.Add(normalizedNameInput)
                        || await db.People.AnyAsync(x => ownerPersonIds.Contains(x.Id)
                            && x.NormalizedFullName == normalizedNameInput, innerCancellationToken);
                    var duplicatePhone = !knownPhones.Add(normalizedPhoneInput)
                        || await db.People.AnyAsync(x => ownerPersonIds.Contains(x.Id)
                            && phoneVariants.Contains(x.NormalizedPhoneNumber), innerCancellationToken);
                    if (duplicateName)
                        errors.Add(new("person.duplicate_name", $"members[{input.DisplayOrder}].fullName", null,
                            "A person with the same normalized name already exists for this owner."));
                    if (duplicatePhone)
                        errors.Add(new("person.duplicate_phone", $"members[{input.DisplayOrder}].phoneNumber", null,
                            "A person with the same normalized phone already exists for this owner."));
                    if (duplicateName || duplicatePhone) continue;

                    string codeValue;
                    do codeValue = codes.Generate();
                    while (!generatedCodes.Add(codeValue) || await db.People.AnyAsync(
                               x => x.SubscriberCode == SubscriberCode.Create(codeValue), innerCancellationToken));
                    person = new Person(input.FullName!, input.PhoneNumber!, SubscriberCode.Create(codeValue),
                        clock.UtcNow);
                    await db.AddEntityAsync(person, innerCancellationToken);
                }

                await personAccess.GrantOwnerAccessAsync(associationOwnerId, person.Id, person.NormalizedFullName,
                    person.NormalizedPhoneNumber, innerCancellationToken);
                if (association.Memberships.Any(x => x.PersonId == person.Id))
                {
                    errors.Add(new("membership.duplicate", $"members[{input.DisplayOrder}].personId", null,
                        "A person cannot be added to the association more than once."));
                    continue;
                }
                var membership = association.AddMember(person.Id, input.ShareCount, MembershipKind.Paying,
                    clock.UtcNow, input.DisplayOrder);
                if (existingAssociation is not null)
                    await db.AddEntityAsync(membership, innerCancellationToken);
                memberships[input.ClientKey] = membership;
                people[input.ClientKey] = person;
            }

            foreach (var input in request.Payouts)
            {
                var period = association.Periods.SingleOrDefault(x => x.PeriodNumber == input.PeriodNumber);
                if (period is null)
                {
                    errors.Add(new("period.not_found", "payouts", input.PeriodNumber,
                        "The payout period is outside the association duration."));
                    continue;
                }
                AssociationMembership? membership = null;
                if (input.RecipientType == PayoutRecipientType.Membership
                    && (input.MemberClientKey is null || !memberships.TryGetValue(input.MemberClientKey, out membership)))
                {
                    errors.Add(new("payout.membership_required", "payouts", input.PeriodNumber,
                        "The payout references a member that is not part of this association."));
                    continue;
                }
                Guid? beneficiaryPersonId = null;
                if (!string.IsNullOrWhiteSpace(input.BeneficiaryMemberClientKey))
                {
                    if (!people.TryGetValue(input.BeneficiaryMemberClientKey, out var beneficiary))
                    {
                        errors.Add(new("payout.beneficiary_not_found", "payouts", input.PeriodNumber,
                            "The virtual slot beneficiary is not an association member."));
                        continue;
                    }
                    beneficiaryPersonId = beneficiary.Id;
                }
                var payout = association.AddPayout(period.Id, input.RecipientType, membership?.Id, input.ShareCount,
                    clock.Today, clock.UtcNow, beneficiaryPersonId);
                if (existingAssociation is not null)
                    await db.AddEntityAsync(payout, innerCancellationToken);
            }

            if (errors.Count != 0) throw new AssociationBusinessValidationException(errors);
            if (request.StartAssociation) association.Start(clock.UtcNow);
            await db.SaveChangesAsync(innerCancellationToken);
            var names = people.Values.DistinctBy(x => x.Id).ToDictionary(x => x.Id, x => x.FullName);
            return AssociationMappings.ToDetails(association, names);
        }, cancellationToken);

        logger.LogInformation("Association {AssociationId} configuration saved atomically with {MemberCount} members and {PayoutCount} payouts; start={Start}",
            result.Id, result.Memberships.Count, result.Payouts.Count, request.StartAssociation);
        await audit.WriteAsync(request.AssociationId.HasValue ? "AssociationDraftSaved" : "AssociationCreated",
            nameof(Association), result.Id.ToString(), previousConfiguration,
            new AssociationConfigurationAudit(result.Name, result.StartDate, result.NumberOfMonths,
                result.MonthlyContribution, result.Memberships.Count, result.Payouts.Count, result.Status.ToString()),
            cancellationToken);
        return result;
    }

    private sealed record AssociationConfigurationAudit(string Name, DateOnly StartDate, int NumberOfMonths,
        decimal MonthlyContribution, int MemberCount, int PayoutCount, string Status);
}

public sealed record GetAssociationEditorQuery(Guid AssociationId) : IRequest<AssociationEditorDto>;

public sealed class GetAssociationEditorHandler(ISender sender)
    : IRequestHandler<GetAssociationEditorQuery, AssociationEditorDto>
{
    public async Task<AssociationEditorDto> Handle(GetAssociationEditorQuery request, CancellationToken cancellationToken)
    {
        // The details query is a single no-tracking projection tailored for the editor. Totals and the
        // validation summary are calculated from that immutable snapshot, so opening the editor cannot mutate the draft.
        var association = await sender.Send(new GetAssociationByIdQuery(request.AssociationId), cancellationToken);
        var payingMembers = association.Memberships
            .Where(x => x.Kind == MembershipKind.Paying && x.Status == MembershipStatus.Active).ToList();
        var memberShares = payingMembers.Sum(x => x.ShareCount);
        var scheduledShares = association.Payouts
            .Where(x => x.RecipientType == PayoutRecipientType.Membership).Sum(x => x.ShareCount);
        var errors = BuildValidationSummary(association, payingMembers, memberShares, scheduledShares);
        return new AssociationEditorDto(association, memberShares, scheduledShares,
            association.MonthlyContribution * association.NumberOfMonths,
            memberShares * association.MonthlyContribution, errors);
    }

    private static List<AssociationEditorValidationDto> BuildValidationSummary(
        AssociationDetailsDto association, List<MembershipDto> payingMembers,
        decimal memberShares, decimal scheduledShares)
    {
        var errors = new List<AssociationEditorValidationDto>();
        if (association.Periods.Count != association.NumberOfMonths)
            errors.Add(new("association.period_count", "numberOfMonths", null,
                "عدد الفترات لا يساوي عدد أشهر الجمعية."));
        if (payingMembers.Count == 0)
            errors.Add(new("association.members_required", "members", null,
                "أضف عضوًا دافعًا واحدًا على الأقل."));
        foreach (var period in association.Periods.Where(period =>
                     association.Payouts.All(payout => payout.PeriodId != period.Id)))
            errors.Add(new("association.period_payout_required", "payouts", period.PeriodNumber,
                $"أضف مستلمًا واحدًا على الأقل للدور رقم {period.PeriodNumber}."));
        foreach (var member in payingMembers)
        {
            var assigned = association.Payouts.Where(x => x.RecipientType == PayoutRecipientType.Membership
                && x.MembershipId == member.Id).Sum(x => x.ShareCount);
            if (assigned != member.ShareCount)
                errors.Add(new("association.member_payout_mismatch", "payouts", null,
                    $"وزّع {member.ShareCount:0.##} حصة كاملة للعضو {member.PersonName}."));
        }
        foreach (var payout in association.Payouts.Where(x =>
                     x.RecipientType is PayoutRecipientType.ReserveSlot or PayoutRecipientType.AdvanceSlot
                     && (x.ShareCount != 1m || x.MembershipId.HasValue)))
            errors.Add(new("payout.virtual_full_share", "payouts", payout.PeriodNumber,
                "دور بدون اسم أو سلف يجب أن يكون حصة كاملة ولا يرتبط بعضو."));
        if (memberShares != scheduledShares)
            errors.Add(new("association.financial_imbalance", "payouts", null,
                $"إجمالي حصص الأعضاء ({memberShares:0.##}) يجب أن يساوي الحصص الموزعة عليهم ({scheduledShares:0.##})."));
        return errors;
    }
}
