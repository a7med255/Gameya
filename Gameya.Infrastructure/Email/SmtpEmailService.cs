using Gameya.Application.Abstractions;
using Gameya.Infrastructure.Authentication;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Gameya.Infrastructure.Email;

public sealed class SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string recipient, string subject, string htmlBody, byte[]? attachment,
        string? attachmentName, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var host = string.IsNullOrWhiteSpace(settings.SmtpHost) ? settings.Host : settings.SmtpHost;
        var port = settings.SmtpPort ?? settings.Port;
        var username = string.IsNullOrWhiteSpace(settings.SmtpUsername) ? settings.UserName : settings.SmtpUsername;
        var password = string.IsNullOrWhiteSpace(settings.SmtpPassword) ? settings.Password : settings.SmtpPassword;
        var fromEmail = string.IsNullOrWhiteSpace(settings.FromEmail) ? settings.FromAddress : settings.FromEmail;
        var enableSsl = settings.EnableSsl ?? settings.UseSsl;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("Email transport is not configured.");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(recipient)); message.Subject = subject;
        var body = new BodyBuilder { HtmlBody = htmlBody };
        if (attachment is not null) body.Attachments.Add(attachmentName ?? "attachment", attachment);
        message.Body = body.ToMessageBody();
        using var client = new SmtpClient();
        try
        {
            logger.LogInformation("Sending email of type {EmailSubject}", subject);
            await client.ConnectAsync(host, port, enableSsl, cancellationToken);
            if (!string.IsNullOrWhiteSpace(username))
                await client.AuthenticateAsync(username, password, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogInformation("Email sent successfully for {EmailSubject}", subject);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "External email delivery failed for {EmailSubject}", subject);
            throw;
        }
    }
}
