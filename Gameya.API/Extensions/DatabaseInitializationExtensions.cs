using Gameya.Application.Common;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gameya.API.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment, CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.IsRelational() &&
            (environment.IsDevelopment() || configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup")))
            await db.Database.MigrateAsync(cancellationToken);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AppRoles.Admin, AppRoles.Owner, AppRoles.PaymentUser })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));

        var adminEmail = configuration["BootstrapAdmin:Email"];
        var adminPassword = configuration["BootstrapAdmin:Password"];
        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await users.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true,
                    DisplayName = "Platform Administrator", CreatedAtUtc = DateTimeOffset.UtcNow };
                var result = await users.CreateAsync(admin, adminPassword);
                if (!result.Succeeded) throw new InvalidOperationException("Bootstrap administrator could not be created.");
                var roleResult = await users.AddToRoleAsync(admin, AppRoles.Admin);
                if (!roleResult.Succeeded)
                {
                    await users.DeleteAsync(admin);
                    throw new InvalidOperationException("Bootstrap administrator role could not be assigned.");
                }
            }
        }
    }
}
