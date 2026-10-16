using Gameya.Application.Common;
using Gameya.Application.Features.Associations;
using Gameya.Application.Features.Authentication;
using Gameya.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/associations"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class AssociationsController(ISender sender) : ControllerBase
{
    [HttpPost, Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Create(CreateAssociationCommand command, CancellationToken cancellationToken)
    { var result = await sender.Send(command, cancellationToken); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpPost("configuration"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> CreateConfiguration(SaveAssociationConfigurationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(request.ToCommand(null), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] AssociationStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetAssociationsQuery(search, status, page, pageSize), cancellationToken));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await sender.Send(new GetAssociationByIdQuery(id), cancellationToken));
    [HttpGet("{id:guid}/editor")]
    public async Task<IActionResult> Editor(Guid id, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetAssociationEditorQuery(id), cancellationToken));
    [HttpPut("{id:guid}/configuration"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> SaveConfiguration(Guid id, SaveAssociationConfigurationRequest request,
        CancellationToken cancellationToken) => Ok(await sender.Send(request.ToCommand(id), cancellationToken));
    [HttpPost("{id:guid}/members"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> AddMember(Guid id, AddMemberRequest request, CancellationToken cancellationToken)
    { var memberId = await sender.Send(new AddMemberCommand(id, request.PersonId, request.ShareCount, request.Kind), cancellationToken); return Created($"/api/associations/{id}/members/{memberId}", new { id = memberId }); }
    [HttpPatch("{id:guid}/members/{memberId:guid}/shares")]
    public async Task<IActionResult> ChangeMemberShares(Guid id, Guid memberId, ChangeMemberSharesRequest request,
        CancellationToken cancellationToken)
    { await sender.Send(new ChangeMemberSharesCommand(id, memberId, request.ShareCount), cancellationToken); return NoContent(); }
    [HttpDelete("{id:guid}/members/{memberId:guid}"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId, CancellationToken cancellationToken)
    { await sender.Send(new RemoveMemberCommand(id, memberId), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/start"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken) { await sender.Send(new StartAssociationCommand(id), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/pause"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken) { await sender.Send(new PauseAssociationCommand(id), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/cancel"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken) { await sender.Send(new CancelAssociationCommand(id), cancellationToken); return NoContent(); }
    [HttpPost("bulk-cancel"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> BulkCancel(BulkCancelRequest request, CancellationToken cancellationToken)
    { await sender.Send(new BulkCancelAssociationsCommand(request.Ids), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken) { await sender.Send(new ResumeAssociationCommand(id), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/periods/{periodId:guid}/close"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> ClosePeriod(Guid id, Guid periodId, CancellationToken cancellationToken) { await sender.Send(new ClosePeriodCommand(id, periodId), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/complete"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Complete(Guid id, CancellationToken cancellationToken) { await sender.Send(new CompleteAssociationCommand(id), cancellationToken); return NoContent(); }
    [HttpGet("{id:guid}/payouts")]
    public async Task<IActionResult> Payouts(Guid id, CancellationToken cancellationToken) => Ok((await sender.Send(new GetAssociationByIdQuery(id), cancellationToken)).Payouts);
    [HttpPost("{id:guid}/payouts"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> AddPayout(Guid id, AddPayoutRequest request, CancellationToken cancellationToken)
    { var payoutId = await sender.Send(new AddPayoutCommand(id, request.PeriodId, request.RecipientType, request.MembershipId, request.ShareCount), cancellationToken); return Created($"/api/associations/{id}/payouts/{payoutId}", new { id = payoutId }); }
    [HttpPut("{id:guid}/payouts/{payoutId:guid}"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> MovePayout(Guid id, Guid payoutId, MovePayoutRequest request, CancellationToken cancellationToken)
    { await sender.Send(new MovePayoutCommand(id, payoutId, request.PeriodId, request.Reason), cancellationToken); return NoContent(); }
    [HttpDelete("{id:guid}/payouts/{payoutId:guid}"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> RemovePayout(Guid id, Guid payoutId, [FromQuery] string? reason, CancellationToken cancellationToken)
    { await sender.Send(new RemovePayoutCommand(id, payoutId, reason), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/payouts/{payoutId:guid}/complete"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> CompletePayout(Guid id, Guid payoutId, CancellationToken cancellationToken)
    { await sender.Send(new CompletePayoutCommand(id, payoutId), cancellationToken); return NoContent(); }
    [HttpPut("{id:guid}/payouts/{payoutId:guid}/reserve-beneficiary"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> ReserveBeneficiary(Guid id, Guid payoutId, ReserveBeneficiaryRequest request, CancellationToken cancellationToken)
    { await sender.Send(new AssociateReserveBeneficiaryCommand(id, payoutId, request.PersonId), cancellationToken); return NoContent(); }
    [HttpPost("{id:guid}/members/{memberId:guid}/replace"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Replace(Guid id, Guid memberId, ReplaceMemberRequest request, CancellationToken cancellationToken)
    { var replacementId = await sender.Send(new ReplaceMemberCommand(id, memberId, request.Replacements, request.Reason), cancellationToken); return Ok(new { id = replacementId }); }
    [HttpPost("invitations"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Invite(InvitePaymentUserCommand command, CancellationToken cancellationToken) => Accepted(new { id = await sender.Send(command, cancellationToken) });
}

public sealed record AddMemberRequest(Guid PersonId, decimal ShareCount, MembershipKind Kind);
public sealed record ChangeMemberSharesRequest(decimal ShareCount);
public sealed record AddPayoutRequest(Guid PeriodId, PayoutRecipientType RecipientType, Guid? MembershipId, decimal ShareCount);
public sealed record MovePayoutRequest(Guid PeriodId, string? Reason);
public sealed record ReserveBeneficiaryRequest(Guid PersonId);
public sealed record ReplaceMemberRequest(IReadOnlyCollection<ReplacementPersonInput> Replacements, string? Reason);
public sealed record BulkCancelRequest(IReadOnlyCollection<Guid> Ids);
public sealed record SaveAssociationConfigurationRequest(string Name, DateOnly StartDate, int NumberOfMonths,
    decimal MonthlyContribution, IReadOnlyCollection<AssociationEditorMemberInput> Members,
    IReadOnlyCollection<AssociationEditorPayoutInput> Payouts, bool StartAssociation)
{
    public SaveAssociationConfigurationCommand ToCommand(Guid? associationId) =>
        new(associationId, Name, StartDate, NumberOfMonths, MonthlyContribution, Members, Payouts,
            StartAssociation);
}
