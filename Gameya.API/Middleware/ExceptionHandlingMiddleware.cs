using Gameya.Application.Common;
using Gameya.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gameya.API.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            var (status, code, title) = exception switch
            {
                RequestValidationException => (StatusCodes.Status400BadRequest, "validation.failed", "Validation failed"),
                AuthenticationException e => (StatusCodes.Status401Unauthorized, e.Code, "Authentication failed"),
                ForbiddenException e => (StatusCodes.Status403Forbidden, e.Code, "Forbidden"),
                NotFoundException e => (StatusCodes.Status404NotFound, e.Code, "Not found"),
                ConflictException e => (StatusCodes.Status409Conflict, e.Code, "Conflict"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency.conflict", "Concurrency conflict"),
                DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code, "Business rule violation"),
                _ => (StatusCodes.Status500InternalServerError, "server.error", "Unexpected error")
            };
            if (status >= 500) logger.LogError(exception, "Unhandled request failure for {Path}", context.Request.Path);
            else logger.LogWarning("Request rejected with {Code} for {Path}", code, context.Request.Path);
            var arabic = context.Request.GetTypedHeaders().AcceptLanguage?.Any(x => x.Value.Value?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) == true) == true;
            var problem = new ProblemDetails
            {
                Status = status, Title = arabic ? ErrorMessages.ArabicTitle(code, title) : title,
                Detail = arabic ? ErrorMessages.Arabic(code) : SafeDetail(exception, status),
                Type = $"https://api.gameya.example/problems/{code}", Instance = context.Request.Path
            };
            problem.Extensions["code"] = code; problem.Extensions["traceId"] = context.TraceIdentifier;
            if (exception is RequestValidationException validation)
                problem.Extensions["errors"] = validation.Errors.ToDictionary(x => x.Key,
                    x => x.Value.Select(value => arabic ? ErrorMessages.Arabic(value) : ErrorMessages.English(value)).ToArray());
            context.Response.StatusCode = status; context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static string SafeDetail(Exception exception, int status) => status >= 500 ? "An unexpected error occurred." : exception.Message;
}

internal static class ErrorMessages
{
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["association.name_required"] = "Association name is required.", ["association.invalid_months"] = "Number of months is invalid.",
        ["association.invalid_contribution"] = "Monthly contribution is invalid.", ["person.name_required"] = "Person name is required.",
        ["person.phone_required"] = "Phone number is required.", ["person.invalid_phone"] = "Phone number is invalid.",
        ["membership.invalid_shares"] = "Share count is invalid.", ["payment.invalid_amount"] = "Payment amount is invalid.",
        ["payment.invalid_method"] = "Payment method is invalid.", ["payment.adjustment_reason"] = "An adjustment reason is required.",
        ["payment.adjustment_total_mismatch"] = "Adjustment entries must equal the adjustment amount.",
        ["auth.invalid_email"] = "Email address is invalid.", ["auth.weak_password"] = "Password does not meet the minimum length.",
        ["auth.password_required"] = "Password is required.", ["auth.associations_required"] = "At least one association is required."
    };
    private static readonly IReadOnlyDictionary<string, string> Ar = new Dictionary<string, string>
    {
        ["validation.failed"] = "يوجد خطأ في البيانات المدخلة.", ["server.error"] = "حصل خطأ غير متوقع.",
        ["authorization.forbidden"] = "غير مسموح لك بتنفيذ العملية دي.", ["association.not_found"] = "الجمعية غير موجودة.",
        ["person.not_found"] = "الشخص غير موجود.", ["payment.not_found"] = "الدفعة غير موجودة.",
        ["auth.invalid_credentials"] = "بيانات الدخول غير صحيحة.", ["auth.invalid_participant_credentials"] = "رقم الموبايل أو كود المشترك غير صحيح.",
        ["auth.email_not_confirmed"] = "لازم تأكد البريد الإلكتروني الأول.", ["person.duplicate_phone"] = "رقم الموبايل مسجل قبل كده.",
        ["person.duplicate_name"] = "في شخص مسجل بنفس الاسم.", ["association.financial_imbalance"] = "إجمالي التزامات الأعضاء لا يساوي قيمة القبض المجدولة.",
        ["payment.credit_association_required"] = "اختار الجمعية اللي هيتسجل عليها الرصيد الزيادة.", ["concurrency.conflict"] = "البيانات اتغيرت في نفس الوقت؛ حاول تاني.",
        ["association.name_required"] = "اسم الجمعية مطلوب.", ["association.invalid_months"] = "عدد الشهور غير صحيح.",
        ["association.invalid_contribution"] = "قيمة الاشتراك الشهري غير صحيحة.", ["person.name_required"] = "اسم الشخص مطلوب.",
        ["person.phone_required"] = "رقم الموبايل مطلوب.", ["person.invalid_phone"] = "رقم الموبايل غير صحيح.",
        ["membership.invalid_shares"] = "عدد الأسهم غير صحيح.", ["payment.invalid_amount"] = "قيمة الدفعة غير صحيحة."
    };
    public static string Arabic(string code) => Ar.GetValueOrDefault(code, "تعذر تنفيذ الطلب.");
    public static string English(string code) => En.GetValueOrDefault(code, "The value is invalid.");
    public static string ArabicTitle(string code, string fallback) => code == "validation.failed" ? "راجع البيانات" : Arabic(code);
}
