using Gameya.Application.Features.Authentication;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Gameya.API.Controllers;

[ApiController, Route("api/auth"), AllowAnonymous, EnableRateLimiting("auth")]
public sealed class AuthController(ISender sender) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterOwnerCommand command, CancellationToken cancellationToken) => Accepted(await sender.Send(command, cancellationToken));
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string token, CancellationToken cancellationToken)
    { await sender.Send(new ConfirmEmailCommand(userId, token), cancellationToken); return NoContent(); }
    [HttpPost("confirm-email/resend")]
    public async Task<IActionResult> ResendConfirmation(ResendOwnerConfirmationCommand command, CancellationToken cancellationToken)
    { await sender.Send(command, cancellationToken); return NoContent(); }
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginCommand command, CancellationToken cancellationToken) => Ok(await sender.Send(command, cancellationToken));
    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestPasswordReset(RequestPasswordResetCommand command,
        CancellationToken cancellationToken) { await sender.Send(command, cancellationToken); return Accepted(); }
    [HttpPost("password-reset")]
    public async Task<IActionResult> ResetPassword(ResetPasswordCommand command, CancellationToken cancellationToken)
    { await sender.Send(command, cancellationToken); return NoContent(); }
    [HttpPost("participant-login")]
    public async Task<IActionResult> ParticipantLogin(ParticipantLoginCommand command, CancellationToken cancellationToken) => Ok(await sender.Send(command, cancellationToken));
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenCommand command, CancellationToken cancellationToken) => Ok(await sender.Send(command, cancellationToken));
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(LogoutCommand command, CancellationToken cancellationToken) { await sender.Send(command, cancellationToken); return NoContent(); }
    [HttpPost("invitations/accept")]
    public async Task<IActionResult> Accept(AcceptInvitationCommand command, CancellationToken cancellationToken) { await sender.Send(command, cancellationToken); return NoContent(); }
}
