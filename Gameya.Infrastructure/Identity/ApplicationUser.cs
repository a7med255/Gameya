using Microsoft.AspNetCore.Identity;

namespace Gameya.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? UserId { get; set; }
    public Guid? PersonId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public bool IsActive(DateTimeOffset now) => RevokedAtUtc is null && ExpiresAtUtc > now;
}

public sealed class PaymentUserInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? AcceptedAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? RevokedByUserId { get; set; }
    public List<InvitationAssociation> Associations { get; set; } = [];
}

public sealed class InvitationAssociation
{
    public Guid InvitationId { get; set; }
    public Guid AssociationId { get; set; }
}

public sealed class UserAssociationAccess
{
    public string UserId { get; set; } = string.Empty;
    public Guid AssociationId { get; set; }
    public string GrantedByOwnerId { get; set; } = string.Empty;
    public DateTimeOffset GrantedAtUtc { get; set; }
}

public sealed class OwnerPersonAccess
{
    public string OwnerId { get; set; } = string.Empty;
    public Guid PersonId { get; set; }
    public string NormalizedName { get; set; } = string.Empty;
    public string NormalizedPhone { get; set; } = string.Empty;
    public DateTimeOffset GrantedAtUtc { get; set; }
}

public sealed class BackgroundJobExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ExecutionKey { get; set; } = string.Empty;
    public string Status { get; set; } = "Started";
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? LastError { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
