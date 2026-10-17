using System.Security.Cryptography;
using System.Text;
using Gameya.Application.Abstractions;
using Gameya.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Gameya.API.Controllers;

[ApiController, Route("api/internal/scheduled-reports")]
public sealed class ScheduledReportsController(
    IMonthlyReportCoordinator coordinator,
    IOptions<ScheduledReportsOptions> options,
    ILogger<ScheduledReportsController> logger) : ControllerBase
{
    [AllowAnonymous, HttpPost("monthly-collection")]
    public async Task<IActionResult> SendMonthlyCollection(CancellationToken cancellationToken)
    {
        var expected = options.Value.Secret;
        var supplied = Request.Headers["X-Gameya-Scheduler-Key"].ToString();
        if (expected.Length < 32 || supplied.Length != expected.Length ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied)))
        {
            logger.LogWarning("Rejected monthly report scheduler request from {RemoteIp}",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }

        await coordinator.SendCurrentMonthAsync(cancellationToken);
        return NoContent();
    }
}
