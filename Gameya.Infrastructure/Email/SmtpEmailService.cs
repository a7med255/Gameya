using Gameya.Application.Abstractions;
using Gameya.Infrastructure.Authentication;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Gameya.Infrastructure.Email;

public sealed class SmtpEmailService(IOptions<EmailOptions> options) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string htmlBody, byte[]? attachment,
        string? attachmentName, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new InvalidOperationException("Email transport is not configured.");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(recipient)); message.Subject = subject;
        var body = new BodyBuilder { HtmlBody = htmlBody };
        if (attachment is not null) body.Attachments.Add(attachmentName ?? "attachment", attachment);
        message.Body = body.ToMessageBody();
        using var client = new SmtpClient();
        await client.ConnectAsync(settings.Host, settings.Port, settings.UseSsl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(settings.UserName))
            await client.AuthenticateAsync(settings.UserName, settings.Password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
