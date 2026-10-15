using Gameya.Application.Common;
using Gameya.Application.Features.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/payment-users"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class PaymentUsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPaymentUsersQuery(search, page, pageSize), cancellationToken));
    [HttpGet("invitations")]
    public async Task<IActionResult> Invitations([FromQuery] string? search, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPaymentUserInvitationsQuery(search, page, pageSize), cancellationToken));
    [HttpPost("invitations")]
    public async Task<IActionResult> Invite(InvitePaymentUserCommand command, CancellationToken cancellationToken)
    {
        var id = await sender.Send(command, cancellationToken);
        return Created($"/api/payment-users/invitations/{id}", new { id });
    }
    [HttpDelete("invitations/{invitationId:guid}")]
    public async Task<IActionResult> RevokeInvitation(Guid invitationId, CancellationToken cancellationToken)
    { await sender.Send(new RevokePaymentUserInvitationCommand(invitationId), cancellationToken); return NoContent(); }
    [HttpPut("{userId}/associations")]
    public async Task<IActionResult> SetAssociations(string userId, SetPaymentUserAssociationsRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SetPaymentUserAssociationsCommand(userId, request.AssociationIds), cancellationToken);
        return NoContent();
    }
    [HttpDelete("{userId}")]
    public async Task<IActionResult> Deactivate(string userId, CancellationToken cancellationToken)
    { await sender.Send(new DeactivatePaymentUserCommand(userId), cancellationToken); return NoContent(); }
}

public sealed record SetPaymentUserAssociationsRequest(IReadOnlyCollection<Guid> AssociationIds);
