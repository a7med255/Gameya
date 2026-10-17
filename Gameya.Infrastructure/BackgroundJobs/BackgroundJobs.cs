using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Infrastructure.BackgroundJobs;

public sealed class IdempotentReportJob(ApplicationDbContext db, IReportDeliveryService delivery, IClock clock) : IIdempotentReportJob
{
    public async Task ExecuteAsync(string executionKey, string userId, bool includeAllOwners, int year, int month,
        CancellationToken cancellationToken)
    {
        var execution = await db.BackgroundJobExecutions.SingleOrDefaultAsync(x => x.ExecutionKey == executionKey, cancellationToken);
        if (execution?.Status == "Completed") return;
        // A scheduler retry while the first request is still running must not send the same report twice.
        // Failed or stale executions remain safely retryable.
        if (execution?.Status == "Started" && execution.StartedAtUtc > clock.UtcNow.AddMinutes(-30)) return;
        if (execution is null)
        {
            execution = new BackgroundJobExecution { ExecutionKey = executionKey, StartedAtUtc = clock.UtcNow };
            await db.BackgroundJobExecutions.AddAsync(execution, cancellationToken);
        }
        else { execution.Status = "Started"; execution.LastError = null; execution.StartedAtUtc = clock.UtcNow; }
        await db.SaveChangesAsync(cancellationToken);
        try
        {
            await delivery.SendMonthlyAsync(userId, includeAllOwners, year, month, cancellationToken);
            execution.Status = "Completed"; execution.CompletedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            execution.Status = "Failed"; execution.LastError = exception.Message[..Math.Min(exception.Message.Length, 2000)];
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}

public sealed class MonthlyReportCoordinator(UserManager<ApplicationUser> users, IIdempotentReportJob reportJob, IClock clock)
    : IMonthlyReportCoordinator
{
    public async Task SendCurrentMonthAsync(CancellationToken cancellationToken)
    {
        var period = clock.Today;
        var owners = await users.GetUsersInRoleAsync(AppRoles.Owner);
        foreach (var owner in owners.Where(x => x.IsActive))
            await reportJob.ExecuteAsync($"automatic:{owner.Id}:{period:yyyy}{period.Month:D2}", owner.Id, false,
                period.Year, period.Month, cancellationToken);
    }
}
