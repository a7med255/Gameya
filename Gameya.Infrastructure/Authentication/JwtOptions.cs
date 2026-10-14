namespace Gameya.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 30;
}

public sealed class AssociationCalendarOptions
{
    public const string SectionName = "AssociationCalendar";
    public int DueDay { get; init; } = 10;
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";
    public string SmtpHost { get; init; } = string.Empty;
    public int? SmtpPort { get; init; }
    public string SmtpUsername { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public string FromEmail { get; init; } = string.Empty;
    public bool? EnableSsl { get; init; }

    // Backward-compatible aliases for existing installations. New deployments should use the Smtp* keys above.
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Gameya";
    public string PublicApiBaseUrl { get; init; } = string.Empty;
    public string PublicAppBaseUrl { get; init; } = "http://localhost:4200";
}
