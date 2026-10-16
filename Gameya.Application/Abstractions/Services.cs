using Gameya.Application.DTOs;
using Gameya.Application.Common;

namespace Gameya.Application.Abstractions;

public interface ICurrentUser
{
    string? UserId { get; }
    Guid? PersonId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
    string? IpAddress { get; }
}

public interface IClock
{
    DateTimeOffset UtcNow { get; }
    DateOnly Today { get; }
    DateTimeOffset EgyptStartOfDayUtc(DateOnly localDate);
}
public interface ISubscriberCodeGenerator { string Generate(); }

public interface IAssociationCalendar
{
    IReadOnlyCollection<(DateOnly PeriodDate, DateOnly DueDate)> CreatePeriods(DateOnly startDate, int numberOfMonths);
}

public interface IAssociationAccessService
{
    IQueryable<Guid> GetAccessibleAssociationIds(string userId, bool isAdmin, bool isOwner);
    Task EnsureAccessAsync(Guid associationId, CancellationToken cancellationToken);
}

public interface IPersonAccessService
{
    IQueryable<Guid> GetAccessiblePersonIds(string userId, bool isAdmin, bool isOwner);
    Task EnsureAccessAsync(Guid personId, CancellationToken cancellationToken);
    Task GrantOwnerAccessAsync(string ownerId, Guid personId, string normalizedName, string normalizedPhone,
        CancellationToken cancellationToken);
    Task UpdateOwnerAccessAsync(string ownerId, Guid personId, string normalizedName, string normalizedPhone,
        CancellationToken cancellationToken);
}

public interface IIdentityService
{
    Task<RegistrationResultDto> RegisterOwnerAsync(string name, string email, string password, CancellationToken cancellationToken);
    Task ResendOwnerConfirmationAsync(string email, CancellationToken cancellationToken);
    Task ConfirmEmailAsync(string userId, string token, CancellationToken cancellationToken);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken);
    Task ResetPasswordAsync(string userId, string token, string newPassword, CancellationToken cancellationToken);
    Task<AuthTokensDto> LoginAsync(string email, string password, CancellationToken cancellationToken);
    Task<AuthTokensDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
    Task<Guid> InvitePaymentUserAsync(string ownerId, bool isAdmin, string email, IReadOnlyCollection<Guid> associationIds, CancellationToken cancellationToken);
    Task<PagedResult<PaymentUserDto>> GetPaymentUsersAsync(string actorId, bool isAdmin, string? search,
        int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<PaymentUserInvitationDto>> GetPaymentUserInvitationsAsync(string actorId, bool isAdmin,
        string? search, int page, int pageSize, CancellationToken cancellationToken);
    Task RevokePaymentUserInvitationAsync(string actorId, bool isAdmin, Guid invitationId,
        CancellationToken cancellationToken);
    Task DeactivatePaymentUserAsync(string actorId, bool isAdmin, string paymentUserId,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> SetPaymentUserAssociationsAsync(string actorId, bool isAdmin, string paymentUserId,
        IReadOnlyCollection<Guid> associationIds, CancellationToken cancellationToken);
    Task AcceptInvitationAsync(string token, string password, CancellationToken cancellationToken);
    Task<AuthTokensDto> CreateParticipantTokenAsync(Guid personId, string phoneNumber, CancellationToken cancellationToken);
}

public interface IEmailService
{
    Task SendAsync(string recipient, string subject, string htmlBody, byte[]? attachment,
        string? attachmentName, CancellationToken cancellationToken);
}

public interface IReportGenerator
{
    Task<GeneratedReportDto> GenerateMonthlyAsync(string userId, bool includeAllOwners, int year, int month, CancellationToken cancellationToken);
    Task<GeneratedReportDto> GenerateAssociationAsync(string userId, bool includeAllOwners, Guid associationId,
        CancellationToken cancellationToken);
}

public interface IReportDeliveryService
{
    Task SendMonthlyAsync(string userId, bool includeAllOwners, int year, int month, CancellationToken cancellationToken);
    Task SendAssociationAsync(string userId, bool includeAllOwners, Guid associationId,
        CancellationToken cancellationToken);
}

public interface IIdempotentReportJob
{
    Task ExecuteAsync(string executionKey, string userId, bool includeAllOwners, int year, int month, CancellationToken cancellationToken);
}

public interface IMonthlyReportCoordinator
{
    Task SendPreviousMonthAsync(CancellationToken cancellationToken);
}

public interface IAuditService
{
    Task WriteAsync(string action, string entityName, string entityId, object? oldValues, object? newValues,
        CancellationToken cancellationToken);
}
