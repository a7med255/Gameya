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
    public async Task Payment_user_cannot_access_association_configuration()
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
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/associations/{allowed.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/associations/{denied.Id}")).StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_its_replacement()
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        await CreateOwnerAsync(email);
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var loginJson = await login.Content.ReadFromJsonAsync<JsonElement>();
        var firstToken = loginJson.GetProperty("refreshToken").GetString()!;

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = firstToken });
        refresh.EnsureSuccessStatusCode();
        var secondToken = (await refresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("refreshToken").GetString()!;

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = firstToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = secondToken })).StatusCode);
    }

    [Fact]
    public async Task Owner_people_are_tenant_scoped_and_directly_created_people_remain_accessible()
    {
        var firstEmail = $"owner-scope-a-{Guid.NewGuid():N}@example.com";
        var secondEmail = $"owner-scope-b-{Guid.NewGuid():N}@example.com";
        var firstOwnerId = await CreateOwnerAsync(firstEmail);
        var secondOwnerId = await CreateOwnerAsync(secondEmail);
        var firstPerson = new Person("Scoped Person A", $"+204{Random.Shared.Next(10000000, 99999999)}",
            SubscriberCode.Create("SCOP2345"), DateTimeOffset.UtcNow);
        var secondPerson = new Person("Scoped Person B", $"+205{Random.Shared.Next(10000000, 99999999)}",
            SubscriberCode.Create("SCOP6789"), DateTimeOffset.UtcNow);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.PeopleSet.AddRange(firstPerson, secondPerson);
            db.OwnerPersonAccesses.AddRange(
                new OwnerPersonAccess { OwnerId = firstOwnerId, PersonId = firstPerson.Id, GrantedAtUtc = DateTimeOffset.UtcNow },
                new OwnerPersonAccess { OwnerId = secondOwnerId, PersonId = secondPerson.Id, GrantedAtUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var client = await LoginOwnerAsync(firstEmail);

        var people = await client.GetFromJsonAsync<JsonElement>("/api/people?pageSize=100");
        var ids = people.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(firstPerson.Id, ids);
        Assert.DoesNotContain(secondPerson.Id, ids);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/people/{secondPerson.Id}")).StatusCode);

        var createdResponse = await client.PostAsJsonAsync("/api/people", new
        {
            fullName = $"New Scoped {Guid.NewGuid():N}",
            phoneNumber = $"+206{Random.Shared.Next(10000000, 99999999)}"
        });
        createdResponse.EnsureSuccessStatusCode();
        var createdId = (await createdResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/people/{createdId}")).StatusCode);
    }

    [Fact]
    public async Task Validation_errors_support_arabic()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/register")
        {
            Content = JsonContent.Create(new { name = "", email = "not-an-email", password = "x" })
        };
        request.Headers.AcceptLanguage.ParseAdd("ar-EG");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation.failed", json.GetProperty("code").GetString());
        Assert.Contains("البيانات", json.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task People_search_is_server_side_by_phone_and_pagination_returns_navigation_metadata()
    {
        var email = $"owner-pagination-{Guid.NewGuid():N}@example.com";
        await CreateOwnerAsync(email);
        var client = await LoginOwnerAsync(email);
        var uniquePhone = $"+209{Random.Shared.Next(10000000, 99999999)}";
        foreach (var item in new[]
                 {
                     new { fullName = $"Paged A {Guid.NewGuid():N}", phoneNumber = uniquePhone },
                     new { fullName = $"Paged B {Guid.NewGuid():N}", phoneNumber = $"+208{Random.Shared.Next(10000000, 99999999)}" },
                     new { fullName = $"Paged C {Guid.NewGuid():N}", phoneNumber = $"+206{Random.Shared.Next(10000000, 99999999)}" }
                 })
            (await client.PostAsJsonAsync("/api/people", item)).EnsureSuccessStatusCode();

        var page = await client.GetFromJsonAsync<JsonElement>("/api/people?page=1&pageSize=2");
        Assert.Equal(2, page.GetProperty("items").GetArrayLength());
        Assert.True(page.GetProperty("totalPages").GetInt32() >= 2);
        Assert.True(page.GetProperty("hasNext").GetBoolean());
        Assert.False(page.GetProperty("hasPrevious").GetBoolean());
        Assert.Equal(1, page.GetProperty("pageNumber").GetInt32());

        var search = await client.GetFromJsonAsync<JsonElement>(
            $"/api/people?search={Uri.EscapeDataString(uniquePhone.Replace(" ", string.Empty, StringComparison.Ordinal))}&page=1&pageSize=20");
        var match = Assert.Single(search.GetProperty("items").EnumerateArray());
        Assert.Equal(uniquePhone, match.GetProperty("phoneNumber").GetString());
    }

    private static Association NewAssociation(string name)
    {
        var association = new Association(name, new DateOnly(2026, 1, 1), 1, 1_000m, "owner", DateTimeOffset.UtcNow);
        association.SetPeriods([(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10))]);
        return association;
    }

    private async Task<string> CreateOwnerAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Test Owner", CreatedAtUtc = DateTimeOffset.UtcNow
        };
        Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, AppRoles.Owner)).Succeeded);
        return user.Id;
    }

    private async Task<HttpClient> LoginOwnerAsync(string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
