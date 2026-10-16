using Gameya.Application.Common;
using Gameya.Application.Features.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Controllers;

[ApiController, Route("api/reports/monthly"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    { var report = await sender.Send(new GetMonthlyReportQuery(year, month), cancellationToken); return File(report.Content, report.ContentType, report.FileName); }
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] SendMonthlyReportCommand command, CancellationToken cancellationToken) =>
        Ok(new { executionId = await sender.Send(command, cancellationToken) });
}

[ApiController, Route("api/reports/associations"), Authorize(Policy = AppPolicies.OwnerOrAdmin)]
public sealed class AssociationReportsController(ISender sender) : ControllerBase
{
    [HttpGet("{associationId:guid}")]
    public async Task<IActionResult> Get(Guid associationId, CancellationToken cancellationToken)
    {
        var report = await sender.Send(new GetAssociationReportQuery(associationId), cancellationToken);
        return File(report.Content, report.ContentType, report.FileName);
    }

    [HttpPost("{associationId:guid}/send")]
    public async Task<IActionResult> Send(Guid associationId, CancellationToken cancellationToken)
    {
        await sender.Send(new SendAssociationReportCommand(associationId), cancellationToken);
        return NoContent();
    }
}
