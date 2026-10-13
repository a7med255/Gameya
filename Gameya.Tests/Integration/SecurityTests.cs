using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gameya.Application.Common;
using Gameya.Domain.Entities;
using Gameya.Domain.ValueObjects;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Gameya.Tests.Integration;

public sealed class SecurityTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Protected_endpoint_requires_authentication()
    {
        var response = await factory.CreateClient().GetAsync("/api/associations");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registration_requires_email_confirmation_and_sends_email()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new { name = "Owner One", email = $"owner-{Guid.NewGuid():N}@example.com", password = "Strong!Pass123" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("requiresEmailConfirmation").GetBoolean());
        Assert.True(factory.Email.SentCount > 0);
    }

    [Fact]
    public async Task Participant_can_only_use_the_dedicated_self_page()
    {
        var person = new Person("Participant One", "+201000000001", SubscriberCode.Create("ABCD2345"), DateTimeOffset.UtcNow);
        var other = new Person("Participant Two", "+201000000002", SubscriberCode.Create("EFGH6789"), DateTimeOffset.UtcNow);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PeopleSet.AddRange(person, other); await db.SaveChangesAsync();
        }
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/participant-login",
            new { phoneNumber = person.PhoneNumber, subscriberCode = person.SubscriberCode.Value });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/participant/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/people/{other.Id}")).StatusCode);
    }

    [Fact]
    public async Task Payment_user_receives_not_found_for_an_unassigned_association()
    {
        var email = $"payment-{Guid.NewGuid():N}@example.com";
        var allowed = NewAssociation("Allowed"); var denied = NewAssociation("Denied");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Collector", CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AppRoles.PaymentUser)).Succeeded);
            db.AssociationsSet.AddRange(allowed, denied);
            db.UserAssociationAccesses.Add(new UserAssociationAccess { UserId = user.Id, AssociationId = allowed.Id,
                GrantedByOwnerId = "owner", GrantedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/associations/{allowed.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/associations/{denied.Id}")).StatusCode);
    }

    private static Association NewAssociation(string name)
    {
        var association = new Association(name, new DateOnly(2026, 1, 1), 1, 1_000m, "owner", DateTimeOffset.UtcNow);
        association.SetPeriods([(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10))]);
        return association;
    }
}
