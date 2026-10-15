using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gameya.Application.Common;
using Gameya.Domain.Entities;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Gameya.Tests.Integration;

public sealed class PaymentUserInvitationTests(GameyaApiFactory factory) : IClassFixture<GameyaApiFactory>
{
    [Fact]
    public async Task Invitation_is_sent_immediately_in_arabic_and_revoked_token_cannot_be_used()
    {
        var ownerEmail = $"invite-owner-{Guid.NewGuid():N}@example.com";
        var invitedEmail = $"collector-{Guid.NewGuid():N}@example.com";
        var (ownerId, associationId) = await CreateOwnerAndAssociationAsync(ownerEmail);
        var client = await LoginAsync(ownerEmail);

        var response = await client.PostAsJsonAsync("/api/payment-users/invitations",
            new { email = invitedEmail, associationIds = new[] { associationId } });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var invitationId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var sent = Assert.Single(factory.Email.Messages, x => x.Recipient == invitedEmail);
        Assert.Contains("دعوة", sent.Subject, StringComparison.Ordinal);
        Assert.Contains("dir=\"rtl\"", sent.HtmlBody, StringComparison.OrdinalIgnoreCase);
        var tokenMatch = Regex.Match(sent.HtmlBody, @"token=([^&""<]+)");
        Assert.True(tokenMatch.Success);
        var token = Uri.UnescapeDataString(tokenMatch.Groups[1].Value);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/payment-users/invitations?page=1&pageSize=10");
        var invitation = Assert.Single(list.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == invitationId);
        Assert.Equal("Active", invitation.GetProperty("status").GetString());
        Assert.True(invitation.GetProperty("createdAtUtc").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-2));

        Assert.Equal(HttpStatusCode.NoContent,
            (await client.DeleteAsync($"/api/payment-users/invitations/{invitationId}")).StatusCode);
        var accept = await factory.CreateClient().PostAsJsonAsync("/api/auth/invitations/accept",
            new { token, password = "Strong!Pass123" });
        Assert.Equal(HttpStatusCode.Unauthorized, accept.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.PaymentUserInvitations.FindAsync(invitationId);
        Assert.NotNull(stored!.RevokedAtUtc);
        Assert.Equal(ownerId, stored.RevokedByUserId);
    }

    [Fact]
    public async Task Accepted_invitation_is_single_use_and_payment_user_is_listed_for_its_owner()
    {
        var ownerEmail = $"accepted-owner-{Guid.NewGuid():N}@example.com";
        var invitedEmail = $"accepted-collector-{Guid.NewGuid():N}@example.com";
        var (_, associationId) = await CreateOwnerAndAssociationAsync(ownerEmail);
        var client = await LoginAsync(ownerEmail);
        var response = await client.PostAsJsonAsync("/api/payment-users/invitations",
            new { email = invitedEmail, associationIds = new[] { associationId } });
        response.EnsureSuccessStatusCode();
        var sent = Assert.Single(factory.Email.Messages, x => x.Recipient == invitedEmail);
        var token = Uri.UnescapeDataString(Regex.Match(sent.HtmlBody, @"token=([^&""<]+)").Groups[1].Value);

        var anonymous = factory.CreateClient();
        var accepted = await anonymous.PostAsJsonAsync("/api/auth/invitations/accept",
            new { token, password = "Strong!Pass123" });
        Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/auth/invitations/accept",
                new { token, password = "Strong!Pass123" })).StatusCode);

        var users = await client.GetFromJsonAsync<JsonElement>("/api/payment-users?page=1&pageSize=10");
        var paymentUser = Assert.Single(users.GetProperty("items").EnumerateArray(),
            x => x.GetProperty("email").GetString() == invitedEmail);
        Assert.Contains(associationId,
            paymentUser.GetProperty("associationIds").EnumerateArray().Select(x => x.GetGuid()));
    }

    private async Task<(string OwnerId, Guid AssociationId)> CreateOwnerAndAssociationAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var owner = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Invitation Owner",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        Assert.True((await users.CreateAsync(owner, "Strong!Pass123")).Succeeded);
        Assert.True((await users.AddToRoleAsync(owner, AppRoles.Owner)).Succeeded);
        var association = new Association($"Invitation {Guid.NewGuid():N}", new DateOnly(2026, 1, 1),
            1, 1_000m, owner.Id, DateTimeOffset.UtcNow);
        association.SetPeriods([(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10))]);
        db.AssociationsSet.Add(association);
        await db.SaveChangesAsync();
        return (owner.Id, association.Id);
    }

    private async Task<HttpClient> LoginAsync(string email)
    {
        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Strong!Pass123" });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
