using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gameya.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("GAMEYA_DESIGNTIME_CONNECTION")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? ReadConfiguredConnectionString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new ApplicationDbContext(options, NullLogger<ApplicationDbContext>.Instance);
    }

    private static string ReadConfiguredConnectionString()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(currentDirectory, "Gameya.API", "appsettings.json"),
            Path.Combine(currentDirectory, "..", "Gameya.API", "appsettings.json"),
            Path.Combine(currentDirectory, "appsettings.json")
        }.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates.Where(File.Exists))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings)
                && connectionStrings.TryGetProperty("DefaultConnection", out var value)
                && !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString()!;
        }

        throw new InvalidOperationException(
            "No design-time database connection was found. Configure ConnectionStrings__DefaultConnection or GAMEYA_DESIGNTIME_CONNECTION.");
    }
}
