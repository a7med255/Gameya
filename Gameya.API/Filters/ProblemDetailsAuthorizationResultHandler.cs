using Gameya.API.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Gameya.API.Filters;

public sealed class ProblemDetailsAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }
        var status = authorizeResult.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        var code = authorizeResult.Forbidden ? "authorization.forbidden" : "authentication.required";
        var arabic = LocalizationMessages.IsArabic(context.Request);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = arabic
                ? (status == 401 ? "تسجيل الدخول مطلوب." : "غير مسموح لك بتنفيذ العملية دي.")
                : (status == 401 ? "Authentication required" : "Forbidden"),
            Type = $"https://api.gameya.example/problems/{code}",
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
