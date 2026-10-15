using Gameya.Application.Common;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gameya.API.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            if (db.Database.IsRelational())
            {
                logger.LogInformation("Verifying database connectivity...");
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                if (!canConnect)
                {
                    logger.LogWarning("Database connection cannot be established at startup. Skipping startup migration and seeding. Verify ConnectionStrings:DefaultConnection in appsettings or environment variables.");
                    return;
                }

                var applyMigrations = environment.IsDevelopment() || configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup");
                if (applyMigrations)
                {
                    logger.LogInformation("Applying pending EF Core migrations...");
                    await db.Database.MigrateAsync(cancellationToken);
                    logger.LogInformation("EF Core migrations applied successfully.");
                }
            }

            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in new[] { AppRoles.Admin, AppRoles.Owner, AppRoles.PaymentUser })
            {
                if (!await roles.RoleExistsAsync(role))
                {
                    var roleResult = await roles.CreateAsync(new IdentityRole(role));
                    if (roleResult.Succeeded)
                        logger.LogInformation("Initialized identity role: {Role}", role);
                }
            }

            var adminEmail = configuration["BootstrapAdmin:Email"];
            var adminPassword = configuration["BootstrapAdmin:Password"];
            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                if (await users.FindByEmailAsync(adminEmail) is null)
                {
                    var admin = new ApplicationUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true,
                        DisplayName = "Platform Administrator",
                        CreatedAtUtc = DateTimeOffset.UtcNow
                    };
                    var result = await users.CreateAsync(admin, adminPassword);
                    if (!result.Succeeded)
                    {
                        logger.LogWarning("Bootstrap administrator could not be created: {Errors}",
                            string.Join(", ", result.Errors.Select(e => e.Description)));
                    }
                    else
                    {
                        await users.AddToRoleAsync(admin, AppRoles.Admin);
                        logger.LogInformation("Bootstrap administrator created successfully.");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization encountered an error during startup. The application will continue running so diagnostic endpoints and frontend remain available.");
        }
    }
}
