using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Email;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Gameya.Infrastructure.Authentication;

public sealed class IdentityService(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db,
    IEmailService email,
    IClock clock,
    IOptions<JwtOptions> jwtOptions,
    IOptions<EmailOptions> emailOptions) : IIdentityService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    public async Task<RegistrationResultDto> RegisterOwnerAsync(string name, string emailAddress, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = emailAddress.Trim();
        var user = new ApplicationUser { UserName = normalizedEmail, Email = normalizedEmail, DisplayName = name.Trim(), CreatedAtUtc = clock.UtcNow };
        var result = await users.CreateAsync(user, password);
        EnsureSucceeded(result, "auth.registration_failed");
        var roleResult = await users.AddToRoleAsync(user, AppRoles.Owner);
        if (!roleResult.Succeeded)
        {
            await users.DeleteAsync(user);
            EnsureSucceeded(roleResult, "auth.role_failed");
        }
        await SendConfirmationEmailAsync(user, cancellationToken);
        return new RegistrationResultDto(user.Id, true);
    }

    public async Task ResendOwnerConfirmationAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(emailAddress.Trim());
        if (user is null || !user.IsActive || await users.IsEmailConfirmedAsync(user)) return;
        await SendConfirmationEmailAsync(user, cancellationToken);
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var confirmation = await users.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmation));
        var url = $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/auth/confirm-email?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(encoded)}";
        await email.SendAsync(user.Email!, "تأكيد بريدك الإلكتروني في نظام الجمعية",
            ArabicEmailTemplate.Action("تأكيد البريد الإلكتروني",
                "أهلاً بيك. اضغط على الزر لتأكيد بريدك الإلكتروني وتفعيل حسابك.",
                "تأكيد البريد الإلكتروني", url,
                "لو إنت ما أنشأتش الحساب، تجاهل الرسالة دي وماتشاركش الرابط مع أي حد."),
            null, null, cancellationToken);
    }

    public async Task ConfirmEmailAsync(string userId, string token, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId) ?? throw new AuthenticationException("auth.invalid_confirmation", "Invalid confirmation request.");
        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException) { throw new AuthenticationException("auth.invalid_confirmation", "Invalid confirmation request."); }
        EnsureSucceeded(await users.ConfirmEmailAsync(user, decoded), "auth.invalid_confirmation");
    }

    public async Task RequestPasswordResetAsync(string emailAddress, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(emailAddress.Trim());
        if (user is null || !user.IsActive || !await users.IsEmailConfirmedAsync(user)) return;
        var reset = await users.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(reset));
        var url = $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/auth/reset-password?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(encoded)}";
        await email.SendAsync(user.Email!, "إعادة تعيين كلمة المرور في نظام الجمعية",
            ArabicEmailTemplate.Action("إعادة تعيين كلمة المرور",
                "وصلنا طلب لتغيير كلمة مرور حسابك. اضغط على الزر لاختيار كلمة مرور جديدة.",
                "تغيير كلمة المرور", url,
                "لو إنت ما طلبتش تغيير كلمة المرور، تجاهل الرسالة دي وسيظل حسابك كما هو."),
            null, null, cancellationToken);
    }

    public async Task ResetPasswordAsync(string userId, string token, string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId)
            ?? throw new AuthenticationException("auth.invalid_password_reset", "Invalid password reset request.");
        string decoded;
        try { decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token)); }
        catch (FormatException)
        {
            throw new AuthenticationException("auth.invalid_password_reset", "Invalid password reset request.");
        }
        EnsureSucceeded(await users.ResetPasswordAsync(user, decoded, newPassword), "auth.invalid_password_reset");
    }

    public async Task<AuthTokensDto> LoginAsync(string emailAddress, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(emailAddress.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
            throw new AuthenticationException("auth.invalid_credentials", "Invalid credentials.");
        if (!await users.CheckPasswordAsync(user, password))
        {
            await users.AccessFailedAsync(user);
            throw new AuthenticationException("auth.invalid_credentials", "Invalid credentials.");
        }
        if (await users.GetAccessFailedCountAsync(user) > 0)
            await users.ResetAccessFailedCountAsync(user);
        if (!await users.IsEmailConfirmedAsync(user))
            throw new AuthenticationException("auth.email_not_confirmed", "Email confirmation is required.");
        return await IssueUserTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokensDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var outcome = await db.ExecuteSerializableAsync(async innerCt =>
        {
            var stored = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, innerCt);
            if (stored is null || stored.ExpiresAtUtc <= clock.UtcNow)
                return new RefreshOutcome(null, false);
            if (stored.RevokedAtUtc.HasValue)
            {
                var replacementHash = stored.ReplacedByTokenHash;
                var replayDetected = replacementHash is not null;
                while (replacementHash is not null)
                {
                    var replacement = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == replacementHash, innerCt);
                    if (replacement is null) break;
                    replacement.RevokedAtUtc ??= clock.UtcNow;
                    replacementHash = replacement.ReplacedByTokenHash;
                }
                if (replayDetected) await db.SaveChangesAsync(innerCt);
                return new RefreshOutcome(null, replayDetected);
            }
            stored.RevokedAtUtc = clock.UtcNow;
            AuthTokensDto tokens;
            if (stored.UserId is not null)
            {
                var user = await users.FindByIdAsync(stored.UserId);
                if (user is null || !user.IsActive || !await users.IsEmailConfirmedAsync(user))
                {
                    await db.SaveChangesAsync(innerCt);
                    return new RefreshOutcome(null, false);
                }
                tokens = await IssueUserTokensAsync(user, innerCt, false);
            }
            else if (stored.PersonId is Guid personId)
            {
                var person = await db.PeopleSet.AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId && x.IsActive, innerCt);
                if (person is null)
                {
                    await db.SaveChangesAsync(innerCt);
                    return new RefreshOutcome(null, false);
                }
                tokens = await IssueParticipantTokensAsync(person.Id, person.PhoneNumber, innerCt, false);
            }
            else
            {
                await db.SaveChangesAsync(innerCt);
                return new RefreshOutcome(null, false);
            }
            stored.ReplacedByTokenHash = Hash(tokens.RefreshToken);
            await db.SaveChangesAsync(innerCt);
            return new RefreshOutcome(tokens, false);
        }, cancellationToken);
        if (outcome.Tokens is null)
            throw new AuthenticationException(outcome.ReplayDetected ? "auth.refresh_token_reused" : "auth.invalid_refresh_token",
                "Refresh token is invalid or expired.");
        return outcome.Tokens;
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken);
        if (stored is null) return;
        stored.RevokedAtUtc ??= clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> InvitePaymentUserAsync(string ownerId, bool isAdmin, string emailAddress,
        IReadOnlyCollection<Guid> associationIds, CancellationToken cancellationToken)
    {
        var ids = associationIds.Distinct().ToArray();
        if (await db.AssociationsSet.CountAsync(x => ids.Contains(x.Id) && (isAdmin || x.OwnerId == ownerId), cancellationToken) != ids.Length)
            throw new NotFoundException("association.not_found", "One or more associations were not found.");
        var normalizedEmail = emailAddress.Trim();
        if (await users.FindByEmailAsync(normalizedEmail) is not null)
            throw new ConflictException("auth.email_exists", "An account with this email already exists.");
        var now = clock.UtcNow;
        var priorInvitations = await db.PaymentUserInvitations
            .Where(x => x.OwnerId == ownerId && x.Email == normalizedEmail && x.AcceptedAtUtc == null
                && x.RevokedAtUtc == null)
            .ToListAsync(cancellationToken);
        foreach (var prior in priorInvitations)
        {
            prior.RevokedAtUtc = now;
            prior.RevokedByUserId = ownerId;
        }
        var rawToken = CreateOpaqueToken();
        var invitation = new PaymentUserInvitation
        {
            OwnerId = ownerId, Email = normalizedEmail, TokenHash = Hash(rawToken), CreatedAtUtc = now,
            ExpiresAtUtc = null,
            Associations = ids.Select(x => new InvitationAssociation { AssociationId = x }).ToList()
        };
        await db.PaymentUserInvitations.AddAsync(invitation, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        var url = $"{emailOptions.Value.PublicAppBaseUrl.TrimEnd('/')}/payment-user-invitation?token={Uri.EscapeDataString(rawToken)}";
        await email.SendAsync(invitation.Email, "دعوة للانضمام إلى نظام الجمعية",
            ArabicEmailTemplate.Action("دعوة لتسجيل المدفوعات",
                "تمت دعوتك للانضمام إلى فريق تسجيل المدفوعات. الرابط يظل صالحًا حتى قبوله أو إلغائه من المالك، ويُستخدم مرة واحدة فقط.",
                "قبول الدعوة وإنشاء الحساب", url,
                "لو الدعوة مش موجهة ليك، تجاهل الرسالة دي. لا تشارك رابط الدعوة مع أي شخص."),
            null, null, cancellationToken);
        return invitation.Id;
    }

    public async Task<PagedResult<PaymentUserDto>> GetPaymentUsersAsync(string actorId, bool isAdmin,
        string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var paymentUserRoleId = await db.Roles.AsNoTracking()
            .Where(x => x.NormalizedName == "PAYMENTUSER")
            .Select(x => x.Id).SingleAsync(cancellationToken);
        var roleUserIds = db.UserRoles.AsNoTracking().Where(x => x.RoleId == paymentUserRoleId)
            .Select(x => x.UserId);
        var query = db.Users.AsNoTracking().Where(x => roleUserIds.Contains(x.Id));
        if (!isAdmin)
        {
            var manageableAssociations = db.AssociationsSet.AsNoTracking()
                .Where(x => x.OwnerId == actorId).Select(x => x.Id);
            var visibleUserIds = db.UserAssociationAccesses.AsNoTracking()
                .Where(x => manageableAssociations.Contains(x.AssociationId)).Select(x => x.UserId);
            query = query.Where(x => visibleUserIds.Contains(x.Id));
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x => (x.Email != null && x.Email.Contains(value)) || x.DisplayName.Contains(value));
        }
        var total = await query.CountAsync(cancellationToken);
        var userRows = await query.OrderBy(x => x.DisplayName).ThenBy(x => x.Email)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, Email = x.Email ?? string.Empty, x.DisplayName, x.IsActive, x.CreatedAtUtc })
            .ToListAsync(cancellationToken);
        var userIds = userRows.Select(x => x.Id).ToArray();
        var accesses = db.UserAssociationAccesses.AsNoTracking().Where(x => userIds.Contains(x.UserId));
        if (!isAdmin)
        {
            var manageableAssociations = db.AssociationsSet.AsNoTracking()
                .Where(x => x.OwnerId == actorId).Select(x => x.Id);
            accesses = accesses.Where(x => manageableAssociations.Contains(x.AssociationId));
        }
        var accessRows = await accesses.Select(x => new { x.UserId, x.AssociationId }).ToListAsync(cancellationToken);
        var items = userRows.Select(x => new PaymentUserDto(x.Id, x.Email, x.DisplayName, x.IsActive,
            x.CreatedAtUtc, accessRows.Where(a => a.UserId == x.Id).Select(a => a.AssociationId).ToArray())).ToArray();
        return new PagedResult<PaymentUserDto>(items, page, pageSize, total);
    }

    public async Task<PagedResult<PaymentUserInvitationDto>> GetPaymentUserInvitationsAsync(string actorId,
        bool isAdmin, string? search, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.PaymentUserInvitations.AsNoTracking();
        if (!isAdmin) query = query.Where(x => x.OwnerId == actorId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(x => x.Email.Contains(value));
        }
        var total = await query.CountAsync(cancellationToken);
        var invitations = await query.OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize).Include(x => x.Associations)
            .ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        var items = invitations.Select(x => new PaymentUserInvitationDto(x.Id, x.Email, x.CreatedAtUtc,
            x.ExpiresAtUtc, x.AcceptedAtUtc, x.RevokedAtUtc, x.Associations.Select(a => a.AssociationId).ToArray(),
            x.RevokedAtUtc.HasValue ? "Revoked" : x.AcceptedAtUtc.HasValue ? "Accepted"
                : x.ExpiresAtUtc.HasValue && x.ExpiresAtUtc <= now ? "Expired" : "Active")).ToArray();
        return new PagedResult<PaymentUserInvitationDto>(items, page, pageSize, total);
    }

    public async Task RevokePaymentUserInvitationAsync(string actorId, bool isAdmin, Guid invitationId,
        CancellationToken cancellationToken)
    {
        var invitation = await db.PaymentUserInvitations.SingleOrDefaultAsync(
            x => x.Id == invitationId && (isAdmin || x.OwnerId == actorId), cancellationToken)
            ?? throw new NotFoundException("auth.invitation_not_found", "Invitation was not found.");
        if (invitation.AcceptedAtUtc.HasValue)
            throw new ConflictException("auth.invitation_already_accepted",
                "An accepted invitation cannot be revoked; manage account access instead.");
        if (invitation.RevokedAtUtc.HasValue) return;
        invitation.RevokedAtUtc = clock.UtcNow;
        invitation.RevokedByUserId = actorId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivatePaymentUserAsync(string actorId, bool isAdmin, string paymentUserId,
        CancellationToken cancellationToken)
    {
        var paymentUser = await users.FindByIdAsync(paymentUserId);
        if (paymentUser is null || !await users.IsInRoleAsync(paymentUser, AppRoles.PaymentUser))
            throw new NotFoundException("user.not_found", "Payment user was not found.");
        var manageableAssociationIds = isAdmin
            ? db.AssociationsSet.Select(x => x.Id)
            : db.AssociationsSet.Where(x => x.OwnerId == actorId).Select(x => x.Id);
        var accesses = await db.UserAssociationAccesses.Where(x => x.UserId == paymentUserId
                && manageableAssociationIds.Contains(x.AssociationId))
            .ToListAsync(cancellationToken);
        if (accesses.Count == 0)
            throw new NotFoundException("user.not_found", "Payment user was not found.");
        db.UserAssociationAccesses.RemoveRange(accesses);
        var hasOtherAccess = await db.UserAssociationAccesses.AnyAsync(x => x.UserId == paymentUserId
            && !manageableAssociationIds.Contains(x.AssociationId), cancellationToken);
        if (isAdmin || !hasOtherAccess) paymentUser.IsActive = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> SetPaymentUserAssociationsAsync(string actorId, bool isAdmin, string paymentUserId,
        IReadOnlyCollection<Guid> associationIds, CancellationToken cancellationToken)
    {
        var ids = associationIds.Distinct().ToArray();
        if (await db.AssociationsSet.CountAsync(x => ids.Contains(x.Id) && (isAdmin || x.OwnerId == actorId), cancellationToken) != ids.Length)
            throw new NotFoundException("association.not_found", "One or more associations were not found.");
        var paymentUser = await users.FindByIdAsync(paymentUserId);
        if (paymentUser is null || !paymentUser.IsActive || !await users.IsInRoleAsync(paymentUser, AppRoles.PaymentUser))
            throw new NotFoundException("user.not_found", "Payment user was not found.");

        var manageableAssociationIds = isAdmin
            ? db.AssociationsSet.Select(x => x.Id)
            : db.AssociationsSet.Where(x => x.OwnerId == actorId).Select(x => x.Id);
        var existing = await db.UserAssociationAccesses
            .Where(x => x.UserId == paymentUserId && manageableAssociationIds.Contains(x.AssociationId))
            .ToListAsync(cancellationToken);
        db.UserAssociationAccesses.RemoveRange(existing.Where(x => !ids.Contains(x.AssociationId)));
        var existingIds = existing.Select(x => x.AssociationId).ToHashSet();
        foreach (var associationId in ids.Where(x => !existingIds.Contains(x)))
            db.UserAssociationAccesses.Add(new UserAssociationAccess
            {
                UserId = paymentUserId, AssociationId = associationId,
                GrantedByOwnerId = actorId, GrantedAtUtc = clock.UtcNow
            });
        await db.SaveChangesAsync(cancellationToken);
        return existingIds.ToArray();
    }

    public async Task AcceptInvitationAsync(string token, string password, CancellationToken cancellationToken)
    {
        await db.ExecuteSerializableAsync(async innerCt =>
        {
            var hash = Hash(token);
            var invitation = await db.PaymentUserInvitations.Include(x => x.Associations)
                .SingleOrDefaultAsync(x => x.TokenHash == hash, innerCt);
            if (invitation is null)
                throw new AuthenticationException("auth.invalid_invitation", "Invitation is invalid.");
            if (invitation.RevokedAtUtc.HasValue)
                throw new AuthenticationException("auth.invitation_revoked", "Invitation has been revoked.");
            if (invitation.AcceptedAtUtc.HasValue)
                throw new AuthenticationException("auth.invitation_already_accepted", "Invitation has already been accepted.");
            if (invitation.ExpiresAtUtc.HasValue && invitation.ExpiresAtUtc <= clock.UtcNow)
                throw new AuthenticationException("auth.invalid_invitation", "Invitation has expired.");
            var user = new ApplicationUser
            {
                UserName = invitation.Email, Email = invitation.Email, EmailConfirmed = true,
                DisplayName = invitation.Email, CreatedAtUtc = clock.UtcNow
            };
            EnsureSucceeded(await users.CreateAsync(user, password), "auth.invitation_accept_failed");
            var roleResult = await users.AddToRoleAsync(user, AppRoles.PaymentUser);
            if (!roleResult.Succeeded)
            {
                await users.DeleteAsync(user);
                EnsureSucceeded(roleResult, "auth.role_failed");
            }
            foreach (var association in invitation.Associations)
                db.UserAssociationAccesses.Add(new UserAssociationAccess
                {
                    UserId = user.Id, AssociationId = association.AssociationId,
                    GrantedByOwnerId = invitation.OwnerId, GrantedAtUtc = clock.UtcNow
                });
            invitation.AcceptedAtUtc = clock.UtcNow;
            db.AuditLogsSet.Add(new Gameya.Domain.Entities.AuditLog(user.Id, "PaymentUserInvitationAccepted",
                nameof(PaymentUserInvitation), invitation.Id.ToString(), null,
                System.Text.Json.JsonSerializer.Serialize(new { user.Id, Associations = invitation.Associations.Select(x => x.AssociationId) }),
                clock.UtcNow, null));
            await db.SaveChangesAsync(innerCt);
            return true;
        }, cancellationToken);
    }

    public Task<AuthTokensDto> CreateParticipantTokenAsync(Guid personId, string phoneNumber, CancellationToken cancellationToken) =>
        IssueParticipantTokensAsync(personId, phoneNumber, cancellationToken);

    private async Task<AuthTokensDto> IssueUserTokensAsync(ApplicationUser user, CancellationToken cancellationToken, bool save = true)
    {
        var roles = await users.GetRolesAsync(user);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id), new(ClaimTypes.Email, user.Email ?? string.Empty), new(ClaimTypes.Name, user.DisplayName) };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        return await IssueTokensAsync(claims, user.Id, null, cancellationToken, save);
    }

    private Task<AuthTokensDto> IssueParticipantTokensAsync(Guid personId, string phoneNumber, CancellationToken cancellationToken, bool save = true)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"participant:{personId}"), new("person_id", personId.ToString()),
            new(ClaimTypes.Role, AppRoles.Participant), new("phone", phoneNumber)
        };
        return IssueTokensAsync(claims, null, personId, cancellationToken, save);
    }

    private async Task<AuthTokensDto> IssueTokensAsync(IReadOnlyCollection<Claim> claims, string? userId, Guid? personId,
        CancellationToken cancellationToken, bool save)
    {
        var now = clock.UtcNow; var expires = now.AddMinutes(_jwt.AccessTokenMinutes);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(_jwt.Issuer, _jwt.Audience, claims, now.UtcDateTime, expires.UtcDateTime, credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);
        var rawRefresh = CreateOpaqueToken();
        await db.RefreshTokens.AddAsync(new RefreshToken
        {
            UserId = userId, PersonId = personId, TokenHash = Hash(rawRefresh), CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_jwt.RefreshTokenDays)
        }, cancellationToken);
        if (save) await db.SaveChangesAsync(cancellationToken);
        return new AuthTokensDto(accessToken, expires, rawRefresh);
    }

    private static string CreateOpaqueToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(64));
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private sealed record RefreshOutcome(AuthTokensDto? Tokens, bool ReplayDetected);
    private static void EnsureSucceeded(IdentityResult result, string code)
    {
        if (!result.Succeeded) throw new ConflictException(code, string.Join(' ', result.Errors.Select(x => x.Description)));
    }
}
