using Gameya.Application.Common;
using Gameya.Application.Features.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/payment-users"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class PaymentUsersController(ISender sender) : ControllerBase
{
    [HttpPut("{userId}/associations")]
    public async Task<IActionResult> SetAssociations(string userId, SetPaymentUserAssociationsRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SetPaymentUserAssociationsCommand(userId, request.AssociationIds), cancellationToken);
        return NoContent();
    }
}

public sealed record SetPaymentUserAssociationsRequest(IReadOnlyCollection<Guid> AssociationIds);
