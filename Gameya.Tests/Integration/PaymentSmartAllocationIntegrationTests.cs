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

public sealed class PaymentSmartAllocationIntegrationTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    private async Task<(HttpClient Client, Person Person, Association Association)> SetupUserAndAssociationAsync()
    {
        var email = $"owner-alloc-{Guid.NewGuid():N}@example.com";
        var person = new Person("Integration Person", $"+2010{Random.Shared.Next(10000000, 99999999)}",
            SubscriberCode.Create("INTP1234"), DateTimeOffset.UtcNow.AddMonths(-3));
        Association association;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Integration Owner",
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Owner)).Succeeded);

            var currentMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            association = new Association("Integration Association", currentMonth, 2, 1_000m, user.Id,
                DateTimeOffset.UtcNow.AddMonths(-3));
            association.SetPeriods([
                (currentMonth, new DateOnly(currentMonth.Year, currentMonth.Month, 10)),
                (currentMonth.AddMonths(1), new DateOnly(currentMonth.AddMonths(1).Year, currentMonth.AddMonths(1).Month, 10))
            ]);
            var membership = association.AddMember(person.Id, 1m, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-2));
            association.AddPayout(association.Periods.First().Id, PayoutRecipientType.Membership, membership.Id, 1m, DateTimeOffset.UtcNow.AddMonths(-2));
            association.AddPayout(association.Periods.Last().Id, PayoutRecipientType.ReserveSlot, null, 1m, DateTimeOffset.UtcNow.AddMonths(-2));
            association.Start(DateTimeOffset.UtcNow.AddMonths(-1));
            db.PeopleSet.Add(person);
            db.AssociationsSet.Add(association);
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, person, association);
    }

    [Fact]
    public async Task Scenario17_PreviewApi_GeneratesToken_And_RecordPayment_MatchesPreviewExactly()
    {
        var (client, person, association) = await SetupUserAndAssociationAsync();

        // 1. Preview 1,000 payment
        var previewRes = await client.PostAsJsonAsync("/api/payments/preview", new
        {
            personId = person.Id,
            amount = 1_000m
        });
        previewRes.EnsureSuccessStatusCode();
        var preview = await previewRes.Content.ReadFromJsonAsync<JsonElement>();
        var token = preview.GetProperty("previewToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(1_000m, preview.GetProperty("amountReceived").GetDecimal());
        Assert.Equal(1_000m, preview.GetProperty("allocatedAmount").GetDecimal());

        var previewAllocations = preview.GetProperty("allocations").EnumerateArray().ToArray();
        Assert.Single(previewAllocations);
        Assert.Equal(1_000m, previewAllocations[0].GetProperty("allocated").GetDecimal());

        // 2. Record payment with this token
        var recordRes = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = person.Id,
            amount = 1_000m,
            method = PaymentMethod.Cash,
            previewToken = token
        });
        recordRes.EnsureSuccessStatusCode();
        var payment = await recordRes.Content.ReadFromJsonAsync<JsonElement>();

        var paymentAllocations = payment.GetProperty("allocations").EnumerateArray().ToArray();
        Assert.Single(paymentAllocations);
        Assert.Equal(1_000m, paymentAllocations[0].GetProperty("amount").GetDecimal());
        Assert.Equal(association.Periods.First().Id, paymentAllocations[0].GetProperty("periodId").GetGuid());
    }

    [Fact]
    public async Task Scenario15_StalePreviewToken_RejectsWithConflict()
    {
        var (client, person, _) = await SetupUserAndAssociationAsync();

        var recordRes = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = person.Id,
            amount = 1_000m,
            method = PaymentMethod.Cash,
            previewToken = "INVALID_STALE_TOKEN_99999"
        });

        Assert.Equal(System.Net.HttpStatusCode.Conflict, recordRes.StatusCode);
    }

    [Fact]
    public async Task Dashboard_Balances_And_Payment_Allocations_Remain_Consistent()
    {
        var (client, person, _) = await SetupUserAndAssociationAsync();

        // Record a 500 partial payment
        var payRes = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = person.Id,
            amount = 500m,
            method = PaymentMethod.Cash
        });
        payRes.EnsureSuccessStatusCode();

        // Check payment page dues
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/payments/page/{person.Id}");
        Assert.Equal(500m, page.GetProperty("amountDueNow").GetDecimal());

        // Check dashboard
        var dashboard = await client.GetFromJsonAsync<JsonElement>("/api/dashboard");
        Assert.True(dashboard.GetProperty("totalPaid").GetDecimal() >= 500m);
    }
}
