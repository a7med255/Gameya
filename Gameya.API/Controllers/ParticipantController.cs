using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.Features.People;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/participant"), Authorize(Policy = AppPolicies.ParticipantOnly)]
public sealed class ParticipantController(ISender sender, ICurrentUser current) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken) =>
        Ok(await sender.Send(new GetPersonProfileQuery(current.PersonId ?? throw new ForbiddenException(), true), cancellationToken));
}
