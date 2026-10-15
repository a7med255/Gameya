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
    public async Task SendPreviousMonthAsync(CancellationToken cancellationToken)
    {
        var period = clock.UtcNow.AddMonths(-1);
        var owners = await users.GetUsersInRoleAsync(AppRoles.Owner);
        foreach (var owner in owners.Where(x => x.IsActive))
            await reportJob.ExecuteAsync($"automatic:{owner.Id}:{period:yyyyMM}", owner.Id, false,
                period.Year, period.Month, cancellationToken);
    }
}
