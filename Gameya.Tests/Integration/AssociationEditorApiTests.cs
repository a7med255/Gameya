using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Gameya.Application.Common;
using Gameya.Domain.Enums;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gameya.Tests.Integration;

public sealed class AssociationEditorApiTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Start_returns_all_business_errors_and_rolls_back_the_whole_configuration()
    {
        var client = await CreateOwnerClientAsync();
        var name = $"Invalid start {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/associations/configuration", new
        {
            name,
            startDate = "2027-01-01",
            numberOfMonths = 2,
            monthlyContribution = 1_000m,
            members = new[] { new { clientKey = "member-1", personId = (Guid?)null, fullName = $"Member {Guid.NewGuid():N}", phoneNumber = UniquePhone(), shareCount = 1m, displayOrder = 0 } },
            payouts = new[] { new { periodNumber = 1, recipientType = "Membership", memberClientKey = "member-1", shareCount = .5m, beneficiaryMemberClientKey = (string?)null } },
            startAssociation = true
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = json.GetProperty("errors").EnumerateArray().ToArray();
        Assert.Contains(errors, error => error.GetProperty("code").GetString() == "association.period_payout_required"
            && error.GetProperty("periodNumber").GetInt32() == 2);
        Assert.Contains(errors, error => error.GetProperty("code").GetString() == "association.member_payout_mismatch");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.AssociationsSet.AnyAsync(association => association.Name == name));
    }

    [Fact]
    public async Task Aggregate_save_creates_inline_person_and_virtual_advance_atomically()
    {
        var client = await CreateOwnerClientAsync();
        var memberName = $"Real member {Guid.NewGuid():N}";

        var response = await client.PostAsJsonAsync("/api/associations/configuration", new
        {
            name = $"Atomic {Guid.NewGuid():N}",
            startDate = "2027-01-01",
            numberOfMonths = 1,
            monthlyContribution = 1_500m,
            members = new[] { new { clientKey = "member-1", personId = (Guid?)null, fullName = memberName, phoneNumber = UniquePhone(), shareCount = 1m, displayOrder = 0 } },
            payouts = new object[]
            {
                new { periodNumber = 1, recipientType = "Membership", memberClientKey = "member-1", shareCount = 1m, beneficiaryMemberClientKey = (string?)null },
                new { periodNumber = 1, recipientType = "AdvanceSlot", memberClientKey = (string?)null, shareCount = 1m, beneficiaryMemberClientKey = "member-1" }
            },
            startAssociation = true
        });

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Active", json.GetProperty("status").GetString());
        Assert.Single(json.GetProperty("memberships").EnumerateArray());
        Assert.Contains(json.GetProperty("payouts").EnumerateArray(),
            payout => payout.GetProperty("recipientType").GetString() == "AdvanceSlot"
                && payout.GetProperty("membershipId").ValueKind == JsonValueKind.Null);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.PeopleSet.CountAsync(person => person.FullName == memberName));
        Assert.False(await db.PeopleSet.AnyAsync(person => person.FullName == "سلف"));
    }

    [Fact]
    public async Task Saved_draft_can_be_replaced_as_one_aggregate_without_leaving_old_children()
    {
        var client = await CreateOwnerClientAsync();
        var name = $"Editable {Guid.NewGuid():N}";
        var create = await client.PostAsJsonAsync("/api/associations/configuration", new
        {
            name, startDate = "2027-01-01", numberOfMonths = 1, monthlyContribution = 1_000m,
            members = new[] { new { clientKey = "old", personId = (Guid?)null, fullName = $"Old {Guid.NewGuid():N}", phoneNumber = UniquePhone(), shareCount = 1m, displayOrder = 0 } },
            payouts = new[] { new { periodNumber = 1, recipientType = "Membership", memberClientKey = "old", shareCount = 1m, beneficiaryMemberClientKey = (string?)null } },
            startAssociation = false
        });
        create.EnsureSuccessStatusCode();
        var associationId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/associations/{associationId}/configuration", new
        {
            name, startDate = "2027-02-01", numberOfMonths = 2, monthlyContribution = 1_250m,
            members = new[] { new { clientKey = "new", personId = (Guid?)null, fullName = $"New {Guid.NewGuid():N}", phoneNumber = UniquePhone(), shareCount = 1m, displayOrder = 0 } },
            payouts = new[] { new { periodNumber = 2, recipientType = "Membership", memberClientKey = "new", shareCount = 1m, beneficiaryMemberClientKey = (string?)null } },
            startAssociation = false
        });

        update.EnsureSuccessStatusCode();
        var json = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, json.GetProperty("numberOfMonths").GetInt32());
        Assert.Single(json.GetProperty("memberships").EnumerateArray());
        Assert.Single(json.GetProperty("payouts").EnumerateArray());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.MembershipsSet.CountAsync(x => x.AssociationId == associationId));
        Assert.Equal(2, await db.PeriodsSet.CountAsync(x => x.AssociationId == associationId));
        Assert.Equal(1, await db.PayoutsSet.CountAsync(x => x.AssociationId == associationId));

        var audit = await client.GetFromJsonAsync<JsonElement>($"/api/associations/{associationId}/audit?pageSize=50");
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), entry =>
            entry.GetProperty("action").GetString() == "AssociationDraftSaved"
            && entry.GetProperty("actorName").GetString() == "Editor Owner"
            && entry.GetProperty("oldValues").ValueKind == JsonValueKind.String
            && entry.GetProperty("newValues").ValueKind == JsonValueKind.String);
    }

    [Fact]
    public async Task Normalized_association_name_is_unique_per_owner()
    {
        var client = await CreateOwnerClientAsync();
        var suffix = Guid.NewGuid().ToString("N");
        var first = await client.PostAsJsonAsync("/api/associations/configuration", Draft($"Family   {suffix}"));
        first.EnsureSuccessStatusCode();

        var duplicate = await client.PostAsJsonAsync("/api/associations/configuration", Draft($"  family {suffix}  "));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var json = await duplicate.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("association.duplicate_name", json.GetProperty("code").GetString());
    }

    private static object Draft(string name) => new
    {
        name, startDate = "2027-01-01", numberOfMonths = 1, monthlyContribution = 1_000m,
        members = Array.Empty<object>(), payouts = Array.Empty<object>(), startAssociation = false
    };

    private async Task<HttpClient> CreateOwnerClientAsync()
    {
        var email = $"editor-{Guid.NewGuid():N}@example.com";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Editor Owner", CreatedAtUtc = DateTimeOffset.UtcNow };
            Assert.True((await users.CreateAsync(user, "Strong!Pass123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AppRoles.Owner)).Succeeded);
        }
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string UniquePhone() => $"+2010{Random.Shared.NextInt64(10_000_000, 99_999_999)}";
}
