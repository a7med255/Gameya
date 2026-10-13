using Gameya.Application.Common;
using Gameya.Application.Features.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/dashboard"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class DashboardController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(await sender.Send(new GetDashboardQuery(), cancellationToken));
}
