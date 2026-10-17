using Gameya.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gameya.Tests.Integration;

public sealed class GameyaApiFactory : WebApplicationFactory<Program>
{
    public FakeEmailService Email { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Testing:UseInMemoryDatabase", "true");
        builder.UseSetting("Testing:DatabaseName", $"GameyaTests-{Guid.NewGuid():N}");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Testing:UseInMemoryDatabase"] = "true",
                ["Testing:DatabaseName"] = $"GameyaTests-{Guid.NewGuid():N}",
                ["Email:Host"] = "test",
                ["Email:FromAddress"] = "test@gameya.local",
                ["ScheduledReports:Secret"] = "test-scheduler-secret-32-characters"
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Email);
        });
    }
}

public sealed class FakeEmailService : IEmailService
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<SentEmail> _messages = new();
    public int SentCount => _messages.Count;
    public IReadOnlyCollection<SentEmail> Messages => _messages.ToArray();
    public Task SendAsync(string recipient, string subject, string htmlBody, byte[]? attachment,
        string? attachmentName, CancellationToken cancellationToken)
    {
        _messages.Enqueue(new SentEmail(recipient, subject, htmlBody, attachment, attachmentName));
        return Task.CompletedTask;
    }
}

public sealed record SentEmail(string Recipient, string Subject, string HtmlBody, byte[]? Attachment,
    string? AttachmentName);
