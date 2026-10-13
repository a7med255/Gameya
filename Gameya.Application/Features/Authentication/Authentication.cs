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

public sealed record RefreshTokenCommand(string RefreshToken) : IRequest<AuthTokensDto>;
public sealed class RefreshTokenHandler(IIdentityService identity) : IRequestHandler<RefreshTokenCommand, AuthTokensDto>
{
    public Task<AuthTokensDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken) => identity.RefreshAsync(request.RefreshToken, cancellationToken);
}
public sealed record LogoutCommand(string RefreshToken) : IRequest;
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
        RuleFor(x => x.SubscriberCode).Length(8).WithErrorCode("person.invalid_subscriber_code");
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
        var id = await identity.InvitePaymentUserAsync(currentUser.UserId ?? throw new ForbiddenException(), request.Email,
            request.AssociationIds, cancellationToken);
        await audit.WriteAsync("PaymentUserInvited", "PaymentUserInvitation", id.ToString(), null,
            new { request.Email, request.AssociationIds }, cancellationToken);
        return id;
    }
}
public sealed record AcceptInvitationCommand(string Token, string Password) : IRequest;
public sealed class AcceptInvitationHandler(IIdentityService identity) : IRequestHandler<AcceptInvitationCommand>
{
    public async Task Handle(AcceptInvitationCommand request, CancellationToken cancellationToken) => await identity.AcceptInvitationAsync(request.Token, request.Password, cancellationToken);
}
