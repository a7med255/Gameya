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
                ["Email:FromAddress"] = "test@gameya.local"
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
    public int SentCount { get; private set; }
    public Task SendAsync(string recipient, string subject, string htmlBody, byte[]? attachment,
        string? attachmentName, CancellationToken cancellationToken)
    {
        SentCount++;
        return Task.CompletedTask;
    }
}
