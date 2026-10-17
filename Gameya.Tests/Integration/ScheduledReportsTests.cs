using System.Net;
using Gameya.Application.Common;
using Gameya.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Gameya.Tests.Integration;

public sealed class ScheduledReportsTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Scheduler_endpoint_requires_server_secret_and_is_idempotent_per_owner_and_month()
    {
        var email = $"scheduled-{Guid.NewGuid():N}@example.com";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var owner = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Scheduled Owner", CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(owner, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(owner, AppRoles.Owner)).Succeeded);
        }

        var client = factory.CreateClient();
        var rejected = await client.PostAsync("/api/internal/scheduled-reports/monthly-collection", null);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        client.DefaultRequestHeaders.Add("X-Gameya-Scheduler-Key", "test-scheduler-secret-32-characters");
        var before = factory.Email.SentCount;
        var first = await client.PostAsync("/api/internal/scheduled-reports/monthly-collection", null);
        var afterFirst = factory.Email.SentCount;
        var retry = await client.PostAsync("/api/internal/scheduled-reports/monthly-collection", null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.Equal(before + 1, afterFirst);
        Assert.Equal(afterFirst, factory.Email.SentCount);
    }
}
