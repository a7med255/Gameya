using Gameya.Application.Common;
using Gameya.Application.Features.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/dashboard"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class DashboardController(ISender sender, Gameya.Application.Abstractions.IClock clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int? year, [FromQuery] int? month, CancellationToken cancellationToken)
    {
        var period = clock.Today;
        return Ok(await sender.Send(new GetDashboardQuery(year ?? period.Year, month ?? period.Month), cancellationToken));
    }

    [HttpGet("monthly-finance")]
    public async Task<IActionResult> GetMonthlyFinance([FromQuery] int year, [FromQuery] int month,
        CancellationToken cancellationToken) => Ok(await sender.Send(new GetMonthlyFinanceQuery(year, month), cancellationToken));

    [HttpGet("monthly-collections")]
    public async Task<IActionResult> GetMonthlyCollections([FromQuery] int year, [FromQuery] int month,
        [FromQuery] bool overdueOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetMonthlyCollectionsQuery(year, month, overdueOnly, page, pageSize), cancellationToken));

    [HttpGet("monthly-payouts")]
    public async Task<IActionResult> GetMonthlyPayouts([FromQuery] int year, [FromQuery] int month,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await sender.Send(new GetMonthlyPayoutsQuery(year, month, page, pageSize), cancellationToken));
}
