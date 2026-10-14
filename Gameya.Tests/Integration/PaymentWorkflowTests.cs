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

        var excessiveAdjustment = await client.PostAsJsonAsync($"/api/payments/{partialJson.GetProperty("id").GetGuid()}/adjustments", new
        {
            amount = -600m,
            reason = "Invalid excessive correction",
            allocations = new[] { new { associationId = association.Id, periodId = association.Periods.First().Id, amount = -600m } },
            credits = Array.Empty<object>()
        });
        Assert.Equal(System.Net.HttpStatusCode.Conflict, excessiveAdjustment.StatusCode);

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

    [Fact]
    public async Task One_persons_payment_does_not_reduce_another_persons_obligation()
    {
        var email = $"owner-isolation-{Guid.NewGuid():N}@example.com";
        var firstPerson = new Person("Allocation Owner One", $"+202{Random.Shared.Next(10000000, 99999999)}",
            SubscriberCode.Create("ISOL2345"), DateTimeOffset.UtcNow.AddMonths(-3));
        var secondPerson = new Person("Allocation Owner Two", $"+203{Random.Shared.Next(10000000, 99999999)}",
            SubscriberCode.Create("ISOL6789"), DateTimeOffset.UtcNow.AddMonths(-3));
        Association association;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Isolation Owner", CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Owner)).Succeeded);
            var periodDate = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-1);
            association = new Association("Person Allocation Isolation", periodDate, 1, 1_000m, user.Id,
                DateTimeOffset.UtcNow.AddMonths(-3));
            association.SetPeriods([(periodDate, new DateOnly(periodDate.Year, periodDate.Month, 10))]);
            var firstMembership = association.AddMember(firstPerson.Id, 1m, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-2));
            var secondMembership = association.AddMember(secondPerson.Id, 1m, MembershipKind.Paying, DateTimeOffset.UtcNow.AddMonths(-2));
            association.AddPayout(association.Periods.Single().Id, PayoutRecipientType.Membership,
                firstMembership.Id, 1m, DateTimeOffset.UtcNow.AddMonths(-2));
            association.AddPayout(association.Periods.Single().Id, PayoutRecipientType.Membership,
                secondMembership.Id, 1m, DateTimeOffset.UtcNow.AddMonths(-2));
            association.Start(DateTimeOffset.UtcNow.AddMonths(-1));
            db.PeopleSet.AddRange(firstPerson, secondPerson);
            db.AssociationsSet.Add(association);
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var firstPayment = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = firstPerson.Id, amount = 1_000m, method = PaymentMethod.Cash,
            associationIds = new[] { association.Id }, creditAssociationId = (Guid?)null
        });
        firstPayment.EnsureSuccessStatusCode();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/payments/page/{secondPerson.Id}");
        var due = Assert.Single(page.GetProperty("associations").EnumerateArray());
        Assert.Equal(1_000m, due.GetProperty("outstandingAmount").GetDecimal());

        var secondPayment = await client.PostAsJsonAsync("/api/payments", new
        {
            personId = secondPerson.Id, amount = 1_000m, method = PaymentMethod.Cash,
            associationIds = new[] { association.Id }, creditAssociationId = (Guid?)null
        });
        secondPayment.EnsureSuccessStatusCode();
        var json = await secondPayment.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1_000m, Assert.Single(json.GetProperty("allocations").EnumerateArray())
            .GetProperty("amount").GetDecimal());
    }
}
