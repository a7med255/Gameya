using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Gameya.Application.Common;
using Gameya.Domain.Entities;
using Gameya.Domain.Enums;
using Gameya.Domain.ValueObjects;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Gameya.Tests.Integration;

public sealed class ReportTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Manual_monthly_report_is_generated_and_sent_immediately_in_arabic()
    {
        var email = $"report-owner-{Guid.NewGuid():N}@example.com";
        var now = DateTimeOffset.UtcNow;
        Guid associationId = default;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Report Owner", CreatedAtUtc = now };
            Assert.True((await users.CreateAsync(owner, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(owner, AppRoles.Owner)).Succeeded);
            var periodDate = new DateOnly(now.Year, now.Month, 1);
            var person = new Person("صاحب التقرير", $"+2015{Random.Shared.Next(10000000, 99999999)}",
                SubscriberCode.Create("REPO2345"), now.AddMonths(-1));
            var association = new Association("جمعية التقرير", periodDate, 1, 1_500m, owner.Id, now.AddMonths(-1));
            association.SetPeriods([(periodDate, new DateOnly(now.Year, now.Month, 10))]);
            var member = association.AddMember(person.Id, 1m, MembershipKind.Paying, now.AddMonths(-1));
            association.AddPayout(association.Periods.Single().Id, PayoutRecipientType.Membership,
                member.Id, 1m, now.AddMonths(-1));
            association.Start(now.AddDays(-1));
            associationId = association.Id;
            db.PeopleSet.Add(person); db.AssociationsSet.Add(association); await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());
        var before = factory.Email.SentCount;

        var response = await client.PostAsJsonAsync("/api/reports/monthly/send",
            new { year = now.Year, month = now.Month });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before + 1, factory.Email.SentCount);
        var message = factory.Email.Messages.Last(x => x.Recipient == email);
        Assert.Contains("تقرير الجمعية الشهري", message.Subject, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", message.HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(message.Attachment);
        Assert.True(message.Attachment!.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(message.Attachment, 0, 4));
        Assert.Contains("تقرير-الجمعية", message.AttachmentName, StringComparison.Ordinal);

        var associationPdf = await client.GetAsync($"/api/reports/associations/{associationId}");
        Assert.Equal(HttpStatusCode.OK, associationPdf.StatusCode);
        var bytes = await associationPdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        var associationEmail = await client.PostAsync($"/api/reports/associations/{associationId}/send", null);
        Assert.Equal(HttpStatusCode.NoContent, associationEmail.StatusCode);
        Assert.Contains(factory.Email.Messages, x => x.Recipient == email
            && x.Subject.Contains("تقرير جمعية - جمعية التقرير", StringComparison.Ordinal)
            && x.Attachment is { Length: > 1000 });
    }
}
