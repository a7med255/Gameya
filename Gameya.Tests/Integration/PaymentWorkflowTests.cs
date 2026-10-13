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

public sealed class PaymentWorkflowTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Payments_allocate_oldest_first_and_keep_excess_as_association_credit()
    {
        var email = $"owner-payments-{Guid.NewGuid():N}@example.com";
        var person = new Person("Payment Person", $"+201{Random.Shared.Next(10000000, 99999999)}",
            SubscriberCode.Create("PAYM2345"), DateTimeOffset.UtcNow.AddMonths(-4));
        string ownerId;
        Association association;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Payment Owner", CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Owner)).Succeeded);
            ownerId = user.Id;
            var firstMonth = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-2);
            association = new Association("Payment Allocation", firstMonth, 2, 1_000m, ownerId, DateTimeOffset.UtcNow.AddMonths(-4));
            association.SetPeriods([(firstMonth, new DateOnly(firstMonth.Year, firstMonth.Month, 10)),
                (firstMonth.AddMonths(1), new DateOnly(firstMonth.AddMonths(1).Year, firstMonth.AddMonths(1).Month, 10))]);
            var member = association.AddMember(person.Id, 1m, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-3));
            association.AddPayout(association.Periods.First().Id, PayoutRecipientType.Membership, member.Id, 1m, DateTimeOffset.UtcNow.AddMonths(-3));
            association.Start(DateTimeOffset.UtcNow.AddMonths(-2));
            db.PeopleSet.Add(person); db.AssociationsSet.Add(association); await db.SaveChangesAsync();
        }
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var partial = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = person.Id, amount = 500m, method = PaymentMethod.Cash,
            associationIds = new[] { association.Id }, creditAssociationId = (Guid?)null
        });
        partial.EnsureSuccessStatusCode();
        var partialJson = await partial.Content.ReadFromJsonAsync<JsonElement>();
        var firstAllocation = Assert.Single(partialJson.GetProperty("allocations").EnumerateArray());
        Assert.Equal(association.Periods.First().Id, firstAllocation.GetProperty("periodId").GetGuid());
        Assert.Equal(500m, firstAllocation.GetProperty("amount").GetDecimal());

        var overpayment = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = person.Id, amount = 2_500m, method = PaymentMethod.InstaPay,
            associationIds = new[] { association.Id }, creditAssociationId = association.Id
        });
        overpayment.EnsureSuccessStatusCode();
        var payment = await overpayment.Content.ReadFromJsonAsync<JsonElement>();
        var allocations = payment.GetProperty("allocations").EnumerateArray().ToArray();
        Assert.Equal(2, allocations.Length);
        Assert.Equal(association.Periods.First().Id, allocations[0].GetProperty("periodId").GetGuid());
        Assert.Equal(association.Periods.Last().Id, allocations[1].GetProperty("periodId").GetGuid());
        var credit = Assert.Single(payment.GetProperty("credits").EnumerateArray());
        Assert.Equal(1_000m, credit.GetProperty("amount").GetDecimal());
    }
}
