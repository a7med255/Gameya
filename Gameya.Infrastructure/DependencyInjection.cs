using System.Security.Claims;
using System.Text;
using Gameya.Application.Abstractions;
using Gameya.Application.Common;
using Gameya.Infrastructure.Authentication;
using Gameya.Infrastructure.BackgroundJobs;
using Gameya.Infrastructure.Email;
using Gameya.Infrastructure.Identity;
using Gameya.Infrastructure.Persistence;
using Gameya.Infrastructure.Reports;
using Gameya.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;

namespace Gameya.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
        var useInMemory = configuration.GetValue<bool>("Testing:UseInMemoryDatabase");
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (useInMemory) options.UseInMemoryDatabase(configuration["Testing:DatabaseName"] ?? "GameyaTests");
            else options.UseSqlServer(connectionString,
                sql => sql.EnableRetryOnFailure(5).MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName));
        });
        services.AddScoped<IApplicationDbContext>(x => x.GetRequiredService<ApplicationDbContext>());
        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 10; options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true; options.Password.RequireLowercase = true; options.Password.RequireNonAlphanumeric = true;
            options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.User.RequireUniqueEmail = true;
        }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<AssociationCalendarOptions>(configuration.GetSection(AssociationCalendarOptions.SectionName));
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required.");
        if (string.IsNullOrWhiteSpace(jwt.Issuer) || string.IsNullOrWhiteSpace(jwt.Audience))
            throw new InvalidOperationException("JWT issuer and audience are required.");
        if (Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32) throw new InvalidOperationException("JWT signing key must be at least 32 bytes.");
        if (jwt.AccessTokenMinutes <= 0 || jwt.RefreshTokenDays <= 0)
            throw new InvalidOperationException("JWT token lifetimes must be positive.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = jwt.Issuer, ValidateAudience = true, ValidAudience = jwt.Audience,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = ClaimTypes.Name, RoleClaimType = ClaimTypes.Role
            };
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AppPolicies.OwnerOrAdmin, p => p.RequireRole(AppRoles.Owner, AppRoles.Admin));
            options.AddPolicy(AppPolicies.CanRecordPayments, p => p.RequireRole(AppRoles.Owner, AppRoles.Admin, AppRoles.PaymentUser));
            options.AddPolicy(AppPolicies.ParticipantOnly, p => p.RequireRole(AppRoles.Participant));
        });

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>(); services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISubscriberCodeGenerator, SubscriberCodeGenerator>(); services.AddSingleton<IAssociationCalendar, AssociationCalendar>();
        services.AddScoped<IAssociationAccessService, AssociationAccessService>(); services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IPersonAccessService, PersonAccessService>();
        services.AddScoped<IAuditService, AuditService>(); services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddScoped<IReportGenerator, MonthlyReportGenerator>(); services.AddScoped<IReportDeliveryService, ReportDeliveryService>();
        services.AddScoped<IIdempotentReportJob, IdempotentReportJob>();
        services.AddScoped<IMonthlyReportCoordinator, MonthlyReportCoordinator>();

        QuestPDF.Settings.License = LicenseType.Community;
        return services;
    }
}
