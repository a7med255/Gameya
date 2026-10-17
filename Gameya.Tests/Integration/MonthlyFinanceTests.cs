using System.Net.Http.Headers;
using System.Net.Http.Json;
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

public sealed class MonthlyFinanceTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Dashboard_uses_obligation_month_and_payouts_deduct_each_contribution_once()
    {
        var email = $"monthly-{Guid.NewGuid():N}@example.com";
        var selected = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
        var next = selected.AddMonths(1);
        var created = DateTimeOffset.UtcNow.AddMonths(-3);
        var person = new Person("أحمد صاحب أكثر من جمعية",
            $"+2010{Random.Shared.NextInt64(10_000_000, 99_999_999)}", SubscriberCode.Create("MONF2345"), created);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var owner = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Monthly Owner", CreatedAtUtc = created };
            Assert.True((await users.CreateAsync(owner, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(owner, AppRoles.Owner)).Succeeded);

            var first = new Association("الجمعية الأولى", selected, 2, 1_000m, owner.Id, created);
            first.SetPeriods([(selected, new DateOnly(selected.Year, selected.Month, 10)),
                (next, new DateOnly(next.Year, next.Month, 10))]);
            var firstMember = first.AddMember(person.Id, 1, MembershipKind.Paying, created);
            first.AddPayout(first.Periods.First().Id, PayoutRecipientType.Membership, firstMember.Id, 1, created);
            first.AddPayout(first.Periods.Last().Id, PayoutRecipientType.ReserveSlot, null, 1, created);
            first.Start(created);

            var second = new Association("الجمعية الثانية", selected, 1, 500m, owner.Id, created);
            second.SetPeriods([(selected, new DateOnly(selected.Year, selected.Month, 10))]);
            var secondMember = second.AddMember(person.Id, 1, MembershipKind.Paying, created);
            second.AddPayout(second.Periods.Single().Id, PayoutRecipientType.Membership, secondMember.Id, 1, created);
            second.Start(created);

            // Cash arrived in the following month, but the allocation belongs to the selected obligation month.
            var paidAt = new DateTimeOffset(next.Year, next.Month, 5, 12, 0, 0, TimeSpan.Zero);
            var payment = new Payment(person.Id, owner.Id, 600m, PaymentMethod.Cash, paidAt, paidAt);
            payment.Allocate(first.Id, first.Periods.First().Id, 600m,
                first.Periods.First().DueDate, next.AddDays(4));
            db.PeopleSet.Add(person); db.AssociationsSet.AddRange(first, second); db.PaymentsSet.Add(payment);
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString());

        var dashboard = await client.GetFromJsonAsync<JsonElement>(
            $"/api/dashboard?year={selected.Year}&month={selected.Month}");
        Assert.Equal(1_500m, dashboard.GetProperty("totalDue").GetDecimal());
        Assert.Equal(600m, dashboard.GetProperty("totalPaid").GetDecimal());
        Assert.Equal(900m, dashboard.GetProperty("totalRemaining").GetDecimal());
        Assert.Equal(900m, dashboard.GetProperty("totalOverdue").GetDecimal());
        Assert.Equal(40m, dashboard.GetProperty("collectionProgress").GetDecimal());

        var payouts = await client.GetFromJsonAsync<JsonElement>(
            $"/api/dashboard/monthly-payouts?year={selected.Year}&month={selected.Month}");
        var recipient = Assert.Single(payouts.GetProperty("items").EnumerateArray());
        Assert.Equal(2_500m, recipient.GetProperty("scheduledPayouts").GetDecimal());
        Assert.Equal(1_500m, recipient.GetProperty("contributionsDue").GetDecimal());
        Assert.Equal(1_000m, recipient.GetProperty("netPayout").GetDecimal());
        Assert.Equal(2, recipient.GetProperty("contributions").GetArrayLength());

        var collections = await client.GetFromJsonAsync<JsonElement>(
            $"/api/dashboard/monthly-collections?year={selected.Year}&month={selected.Month}");
        Assert.Single(collections.GetProperty("items").EnumerateArray());
    }
}
