using FluentValidation;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Application.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Gameya.Application.Features.Authentication;

public sealed record RegisterOwnerCommand(string Name, string Email, string Password) : IRequest<RegistrationResultDto>;
public sealed class RegisterOwnerValidator : AbstractValidator<RegisterOwnerCommand>
{
    public RegisterOwnerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200).WithErrorCode("auth.name_required");
        RuleFor(x => x.Email).NotEmpty().EmailAddress().WithErrorCode("auth.invalid_email");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).WithErrorCode("auth.weak_password");
    }
}
public sealed class RegisterOwnerHandler(IIdentityService identity) : IRequestHandler<RegisterOwnerCommand, RegistrationResultDto>
{
    public Task<RegistrationResultDto> Handle(RegisterOwnerCommand request, CancellationToken cancellationToken) =>
        identity.RegisterOwnerAsync(request.Name, request.Email, request.Password, cancellationToken);
}

public sealed record ResendOwnerConfirmationCommand(string Email) : IRequest;
public sealed class ResendOwnerConfirmationValidator : AbstractValidator<ResendOwnerConfirmationCommand>
{
    public ResendOwnerConfirmationValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress()
        .WithErrorCode("auth.invalid_email");
}
public sealed class ResendOwnerConfirmationHandler(IIdentityService identity) : IRequestHandler<ResendOwnerConfirmationCommand>
{
    public async Task Handle(ResendOwnerConfirmationCommand request, CancellationToken cancellationToken) =>
        await identity.ResendOwnerConfirmationAsync(request.Email, cancellationToken);
}

public sealed record ConfirmEmailCommand(string UserId, string Token) : IRequest;
public sealed class ConfirmEmailHandler(IIdentityService identity) : IRequestHandler<ConfirmEmailCommand>
{
    public async Task Handle(ConfirmEmailCommand request, CancellationToken cancellationToken) => await identity.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken);
}

public sealed record LoginCommand(string Email, string Password) : IRequest<AuthTokensDto>;
public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress().WithErrorCode("auth.invalid_email"); RuleFor(x => x.Password).NotEmpty().WithErrorCode("auth.password_required"); }
}
public sealed class LoginHandler(IIdentityService identity) : IRequestHandler<LoginCommand, AuthTokensDto>
{
    public Task<AuthTokensDto> Handle(LoginCommand request, CancellationToken cancellationToken) => identity.LoginAsync(request.Email, request.Password, cancellationToken);
}

public sealed record RequestPasswordResetCommand(string Email) : IRequest;
public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress()
        .WithErrorCode("auth.invalid_email");
}
public sealed class RequestPasswordResetHandler(IIdentityService identity)
    : IRequestHandler<RequestPasswordResetCommand>
{
    public async Task Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken) =>
        await identity.RequestPasswordResetAsync(request.Email, cancellationToken);
}

public sealed record ResetPasswordCommand(string UserId, string Token, string NewPassword) : IRequest;
public sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().WithErrorCode("auth.invalid_password_reset");
        RuleFor(x => x.Token).NotEmpty().WithErrorCode("auth.invalid_password_reset");
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(10).WithErrorCode("auth.weak_password");
    }
}
public sealed class ResetPasswordHandler(IIdentityService identity) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand request, CancellationToken cancellationToken) =>
        await identity.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword, cancellationToken);
}

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokensDto>;
public sealed class RefreshTokenValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("auth.refresh_token_required");
}
public sealed class RefreshTokenHandler(IIdentityService identity) : IRequestHandler<RefreshTokenCommand, AuthTokensDto>
{
    public Task<AuthTokensDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) => identity.RefreshAsync(request.RefreshToken, cancellationToken);
}
public sealed record LogoutCommand(string RefreshToken) : IRequest;
public sealed class LogoutValidator : AbstractValidator<LogoutCommand>
{
    public LogoutValidator() => RuleFor(x => x.RefreshToken).NotEmpty().WithErrorCode("auth.refresh_token_required");
}
public sealed class LogoutHandler(IIdentityService identity) : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken) => await identity.RevokeAsync(request.RefreshToken, cancellationToken);
}

public sealed record ParticipantLoginCommand(string PhoneNumber, string SubscriberCode) : IRequest<AuthTokensDto>;
public sealed class ParticipantLoginValidator : AbstractValidator<ParticipantLoginCommand>
{
    public ParticipantLoginValidator()
    {
        RuleFor(x => x.PhoneNumber).NotEmpty().WithErrorCode("person.phone_required");
        RuleFor(x => x.PhoneNumber).MaximumLength(30).WithErrorCode("person.invalid_phone");
        RuleFor(x => x.SubscriberCode).NotEmpty().Matches("^[A-Za-z0-9]{8}$").WithErrorCode("person.invalid_subscriber_code");
    }
}
public sealed class ParticipantLoginHandler(IApplicationDbContext db, IIdentityService identity)
    : IRequestHandler<ParticipantLoginCommand, AuthTokensDto>
{
    public async Task<AuthTokensDto> Handle(ParticipantLoginCommand request, CancellationToken cancellationToken)
    {
        var code = request.SubscriberCode.Trim().ToUpperInvariant();
        var person = await db.People.AsNoTracking().SingleOrDefaultAsync(x => x.PhoneNumber == request.PhoneNumber.Trim()
            && x.IsActive, cancellationToken);
        if (person is null || !string.Equals(person.SubscriberCode.Value, code, StringComparison.Ordinal))
            throw new AuthenticationException("auth.invalid_participant_credentials", "Invalid participant credentials.");
        return await identity.CreateParticipantTokenAsync(person.Id, person.PhoneNumber, cancellationToken);
    }
}

public sealed record InvitePaymentUserCommand(string Email, IReadOnlyCollection<Guid> AssociationIds) : IRequest<Guid>;
public sealed class InvitePaymentUserValidator : AbstractValidator<InvitePaymentUserCommand>
{
    public InvitePaymentUserValidator() { RuleFor(x => x.Email).NotEmpty().EmailAddress().WithErrorCode("auth.invalid_email"); RuleFor(x => x.AssociationIds).NotEmpty().WithErrorCode("auth.associations_required"); }
}
public sealed class InvitePaymentUserHandler(IIdentityService identity, ICurrentUser currentUser, IAuditService audit) : IRequestHandler<InvitePaymentUserCommand, Guid>
{
    public async Task<Guid> Handle(InvitePaymentUserCommand request, CancellationToken cancellationToken)
    {
        var id = await identity.InvitePaymentUserAsync(currentUser.UserId ?? throw new ForbiddenException(),
            currentUser.IsInRole(AppRoles.Admin), request.Email, request.AssociationIds, cancellationToken);
        await audit.WriteAsync("PaymentUserInvited", "PaymentUserInvitation", id.ToString(), null,
            new { request.Email, request.AssociationIds }, cancellationToken);
        return id;
    }
}
public sealed record GetPaymentUsersQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<PaymentUserDto>>;
public sealed class GetPaymentUsersHandler(IIdentityService identity, ICurrentUser currentUser)
    : IRequestHandler<GetPaymentUsersQuery, PagedResult<PaymentUserDto>>
{
    public Task<PagedResult<PaymentUserDto>> Handle(GetPaymentUsersQuery request,
        CancellationToken cancellationToken) => identity.GetPaymentUsersAsync(
        currentUser.UserId ?? throw new ForbiddenException(), currentUser.IsInRole(AppRoles.Admin),
        request.Search, request.Page, request.PageSize, cancellationToken);
}
public sealed record GetPaymentUserInvitationsQuery(string? Search, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<PaymentUserInvitationDto>>;
public sealed class GetPaymentUserInvitationsHandler(IIdentityService identity, ICurrentUser currentUser)
    : IRequestHandler<GetPaymentUserInvitationsQuery, PagedResult<PaymentUserInvitationDto>>
{
    public Task<PagedResult<PaymentUserInvitationDto>> Handle(GetPaymentUserInvitationsQuery request,
        CancellationToken cancellationToken) => identity.GetPaymentUserInvitationsAsync(
        currentUser.UserId ?? throw new ForbiddenException(), currentUser.IsInRole(AppRoles.Admin),
        request.Search, request.Page, request.PageSize, cancellationToken);
}
public sealed record RevokePaymentUserInvitationCommand(Guid InvitationId) : IRequest;
public sealed class RevokePaymentUserInvitationHandler(IIdentityService identity, ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<RevokePaymentUserInvitationCommand>
{
    public async Task Handle(RevokePaymentUserInvitationCommand request, CancellationToken cancellationToken)
    {
        await identity.RevokePaymentUserInvitationAsync(currentUser.UserId ?? throw new ForbiddenException(),
            currentUser.IsInRole(AppRoles.Admin), request.InvitationId, cancellationToken);
        await audit.WriteAsync("PaymentUserInvitationRevoked", "PaymentUserInvitation",
            request.InvitationId.ToString(), null, new { Revoked = true }, cancellationToken);
    }
}
public sealed record DeactivatePaymentUserCommand(string PaymentUserId) : IRequest;
public sealed class DeactivatePaymentUserHandler(IIdentityService identity, ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<DeactivatePaymentUserCommand>
{
    public async Task Handle(DeactivatePaymentUserCommand request, CancellationToken cancellationToken)
    {
        await identity.DeactivatePaymentUserAsync(currentUser.UserId ?? throw new ForbiddenException(),
            currentUser.IsInRole(AppRoles.Admin), request.PaymentUserId, cancellationToken);
        await audit.WriteAsync("PaymentUserAccessRemoved", "ApplicationUser", request.PaymentUserId,
            null, new { IsActive = false }, cancellationToken);
    }
}
public sealed record SetPaymentUserAssociationsCommand(string PaymentUserId,
    IReadOnlyCollection<Guid> AssociationIds) : IRequest;
public sealed class SetPaymentUserAssociationsValidator : AbstractValidator<SetPaymentUserAssociationsCommand>
{
    public SetPaymentUserAssociationsValidator()
    {
        RuleFor(x => x.PaymentUserId).NotEmpty().WithErrorCode("auth.payment_user_required");
        RuleFor(x => x.AssociationIds).NotNull().WithErrorCode("auth.associations_required");
    }
}
public sealed class SetPaymentUserAssociationsHandler(IIdentityService identity, ICurrentUser currentUser,
    IAuditService audit) : IRequestHandler<SetPaymentUserAssociationsCommand>
{
    public async Task Handle(SetPaymentUserAssociationsCommand request, CancellationToken cancellationToken)
    {
        var actorId = currentUser.UserId ?? throw new ForbiddenException();
        var oldAssociationIds = await identity.SetPaymentUserAssociationsAsync(actorId, currentUser.IsInRole(AppRoles.Admin),
            request.PaymentUserId, request.AssociationIds, cancellationToken);
        await audit.WriteAsync("PaymentUserPermissionsChanged", "UserAssociationAccess", request.PaymentUserId,
            new { AssociationIds = oldAssociationIds }, new { request.AssociationIds }, cancellationToken);
    }
}
public sealed record AcceptInvitationCommand(string Token, string Password) : IRequest;
public sealed class AcceptInvitationValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationValidator()
    {
        RuleFor(x => x.Token).NotEmpty().WithErrorCode("auth.invitation_token_required");
        RuleFor(x => x.Password).NotEmpty().MinimumLength(10).WithErrorCode("auth.weak_password");
    }
}
public sealed class AcceptInvitationHandler(IIdentityService identity) : IRequestHandler<AcceptInvitationCommand>
{
    public async Task Handle(AcceptInvitationCommand request, CancellationToken cancellationToken) => await identity.AcceptInvitationAsync(request.Token, request.Password, cancellationToken);
}
