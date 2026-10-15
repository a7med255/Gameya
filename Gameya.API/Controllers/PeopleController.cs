using Gameya.Application.Common;
using Gameya.Application.Features.People;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/people"), Authorize(Roles = AppRoles.Staff)]
public sealed class PeopleController(ISender sender) : ControllerBase
{
    [HttpPost, Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Create(CreatePersonCommand command, CancellationToken cancellationToken)
    { var result = await sender.Send(command, cancellationToken); return CreatedAtAction(nameof(Get), new { id = result.Id }, result); }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetPeopleQuery(search, page, pageSize), cancellationToken));
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken) => Ok(await sender.Send(new GetPersonProfileQuery(id), cancellationToken));
    [HttpPut("{id:guid}"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Update(Guid id, UpdatePersonRequest request, CancellationToken cancellationToken) =>
        Ok(await sender.Send(new UpdatePersonCommand(id, request.FullName, request.PhoneNumber), cancellationToken));
    [HttpDelete("{id:guid}"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    { await sender.Send(new DeactivatePersonCommand(id), cancellationToken); return NoContent(); }
    [HttpGet("{id:guid}/financial-summary")]
    [Authorize(Policy = AppPolicies.OwnerOrAdmin)]
    public async Task<IActionResult> FinancialSummary(Guid id, CancellationToken cancellationToken) => Ok(await sender.Send(new GetPersonProfileQuery(id), cancellationToken));
}

public sealed record UpdatePersonRequest(string FullName, string PhoneNumber);
