using System.Threading.RateLimiting;
using System.Globalization;
using System.Text.Json.Serialization;
using Gameya.API.Extensions;
using Gameya.API.Middleware;
using Gameya.API.Filters;
using Gameya.Application;
using Gameya.Application.Common;
using Gameya.Infrastructure;
using Gameya.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    .ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = actionContext =>
    {
        var request = actionContext.HttpContext.Request;
        var arabic = LocalizationMessages.IsArabic(request);
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = arabic ? LocalizationMessages.ArabicTitle("validation.failed") : "Validation failed",
            Detail = arabic ? LocalizationMessages.Arabic("validation.failed") : "One or more request values are invalid.",
            Type = "https://api.gameya.example/problems/validation.failed",
            Instance = request.Path
        };
        problem.Extensions["code"] = "validation.failed";
        problem.Extensions["traceId"] = actionContext.HttpContext.TraceIdentifier;
        problem.Extensions["errors"] = actionContext.ModelState.Where(x => x.Value?.Errors.Count > 0)
            .ToDictionary(x => x.Key, x => x.Value!.Errors.Select(_ => arabic
                ? LocalizationMessages.Arabic("validation.invalid_value")
                : "The value is invalid.").ToArray());
        return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(problem);
    };
});
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
    ProblemDetailsAuthorizationResultHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "Gameya API", Version = "v1", Description = "Rotating savings association management API" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = []
    });
});
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
{
    if (origins.Length != 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing") &&
    (string.IsNullOrWhiteSpace(app.Configuration["Jwt:SigningKey"]) ||
     app.Configuration["Jwt:SigningKey"]!.StartsWith("CHANGE-ME", StringComparison.Ordinal)))
    throw new InvalidOperationException("Configure Jwt:SigningKey from a production secret store.");

app.UseForwardedHeaders();
app.UseSerilogRequestLogging(options =>
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");
app.UseMiddleware<ExceptionHandlingMiddleware>();
if (!app.Environment.IsDevelopment()) { app.UseHsts(); }
//if (app.Environment.IsDevelopment()) {
    app.UseSwagger(); 
    app.UseSwaggerUI();
//}
app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
app.UseCors("Angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");
await app.Services.InitializeDatabaseAsync(
    app.Configuration,
    app.Environment,
    app.Lifetime.ApplicationStopping);

if (!app.Configuration.GetValue<bool>("Testing:UseInMemoryDatabase"))
{
    var recurringJobManager =
        app.Services.GetRequiredService<IRecurringJobManager>();

    recurringJobManager.AddOrUpdate<Gameya.Application.Abstractions.IMonthlyReportCoordinator>(
        "gameya-monthly-owner-reports",
        x => x.SendPreviousMonthAsync(CancellationToken.None),
        "0 6 1 * *");
}

app.Run();

public partial class Program;
