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
                DbUpdateException => (StatusCodes.Status409Conflict, "persistence.conflict", "Data conflict"),
                DomainException e => (StatusCodes.Status422UnprocessableEntity, e.Code, "Business rule violation"),
                _ => (StatusCodes.Status500InternalServerError, "server.error", "Unexpected error")
            };
            if (status >= 500) logger.LogError(exception, "Unhandled request failure for {Path}", context.Request.Path);
            else logger.LogWarning("Request rejected with {Code} for {Path}", code, context.Request.Path);
            var arabic = LocalizationMessages.IsArabic(context.Request);
            var problem = new ProblemDetails
            {
                Status = status,
                Title = arabic ? LocalizationMessages.ArabicTitle(code) : title,
                Detail = arabic ? LocalizationMessages.Arabic(code) : SafeDetail(exception, status),
                Type = $"https://api.gameya.example/problems/{code}",
                Instance = context.Request.Path
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = context.TraceIdentifier;
            if (exception is RequestValidationException validation)
                problem.Extensions["errors"] = validation.Errors.ToDictionary(x => x.Key,
                    x => x.Value.Select(value => arabic ? LocalizationMessages.Arabic(value) : LocalizationMessages.English(value)).ToArray());
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static string SafeDetail(Exception exception, int status) => status >= 500
        ? "An unexpected error occurred."
        : exception is DbUpdateException ? "The request conflicts with existing data." : exception.Message;
}

internal static class LocalizationMessages
{
    private static readonly IReadOnlyDictionary<string, string> En = new Dictionary<string, string>
    {
        ["association.name_required"] = "Association name is required.",
        ["association.invalid_months"] = "Number of months is invalid.",
        ["association.invalid_contribution"] = "Monthly contribution is invalid.",
        ["person.name_required"] = "Person name is required.",
        ["person.phone_required"] = "Phone number is required.",
        ["person.invalid_phone"] = "Phone number is invalid.",
        ["person.invalid_subscriber_code"] = "Subscriber code is invalid.",
        ["person.required"] = "Person is required.",
        ["membership.invalid_kind"] = "Membership type is invalid.",
        ["membership.invalid_shares"] = "Share count is invalid.",
        ["payout.invalid_shares"] = "Payout share count is invalid.",
        ["payout.invalid_recipient_type"] = "Payout recipient type is invalid.",
        ["payout.membership_required"] = "A member payout requires a membership.",
        ["payout.reserve_full_share"] = "An unnamed reserve must be exactly one full share.",
        ["payout.reason_too_long"] = "The payout change reason is too long.",
        ["payment.invalid_amount"] = "Payment amount is invalid.",
        ["payment.invalid_method"] = "Payment method is invalid.",
        ["payment.zero_amount"] = "Payment amount cannot be zero.",
        ["payment.adjustment_reason"] = "An adjustment reason is required.",
        ["payment.adjustment_entries_required"] = "Adjustment entries are required.",
        ["payment.duplicate_adjustment_entry"] = "An adjustment entry was supplied more than once.",
        ["payment.adjustment_total_mismatch"] = "Adjustment entries must equal the adjustment amount.",
        ["replacement.required"] = "At least one replacement person is required.",
        ["replacement.duplicate_person"] = "A replacement person cannot be supplied more than once.",
        ["replacement.reason_too_long"] = "The replacement reason is too long.",
        ["report.invalid_period"] = "Report year or month is invalid.",
        ["auth.name_required"] = "Name is required.",
        ["auth.invalid_email"] = "Email address is invalid.",
        ["auth.weak_password"] = "Password does not meet the security requirements.",
        ["auth.password_required"] = "Password is required.",
        ["auth.refresh_token_required"] = "Refresh token is required.",
        ["auth.invitation_token_required"] = "Invitation token is required.",
        ["auth.payment_user_required"] = "Payment user is required.",
        ["auth.associations_required"] = "Association selection is required."
    };

    private static readonly IReadOnlyDictionary<string, string> Ar = new Dictionary<string, string>
    {
        ["validation.failed"] = "البيانات المدخلة فيها أخطاء.",
        ["validation.invalid_value"] = "القيمة المدخلة غير صحيحة.",
        ["server.error"] = "حصل خطأ غير متوقع. حاول مرة أخرى.",
        ["persistence.conflict"] = "البيانات تتعارض مع بيانات مسجلة بالفعل.",
        ["concurrency.conflict"] = "البيانات اتغيرت في نفس الوقت؛ حاول تاني.",
        ["authentication.required"] = "لازم تسجل الدخول الأول.",
        ["authorization.forbidden"] = "غير مسموح لك بتنفيذ العملية دي.",
        ["auth.invalid_credentials"] = "بيانات الدخول غير صحيحة.",
        ["auth.invalid_participant_credentials"] = "رقم الموبايل أو كود المشترك غير صحيح.",
        ["auth.email_not_confirmed"] = "لازم تأكد البريد الإلكتروني الأول.",
        ["auth.invalid_refresh_token"] = "جلسة الدخول غير صالحة أو انتهت.",
        ["auth.refresh_token_reused"] = "تم إلغاء جلسة الدخول لحمايتك. سجل الدخول من جديد.",
        ["auth.invalid_invitation"] = "الدعوة غير صالحة أو انتهت.",
        ["auth.invalid_confirmation"] = "رابط تأكيد البريد غير صالح.",
        ["auth.name_required"] = "الاسم مطلوب.",
        ["auth.invalid_email"] = "البريد الإلكتروني غير صحيح.",
        ["auth.weak_password"] = "كلمة المرور لا تحقق شروط الأمان.",
        ["auth.password_required"] = "كلمة المرور مطلوبة.",
        ["auth.refresh_token_required"] = "رمز تجديد الجلسة مطلوب.",
        ["auth.invitation_token_required"] = "رمز الدعوة مطلوب.",
        ["auth.payment_user_required"] = "مستخدم التحصيل مطلوب.",
        ["auth.associations_required"] = "اختيار جمعية مطلوب.",
        ["association.not_found"] = "الجمعية غير موجودة.",
        ["association.name_required"] = "اسم الجمعية مطلوب.",
        ["association.invalid_months"] = "عدد الشهور غير صحيح.",
        ["association.invalid_contribution"] = "قيمة الاشتراك الشهري غير صحيحة.",
        ["association.financial_imbalance"] = "التزامات كل عضو دافع لا تطابق قيمة قبضه المجدولة.",
        ["association.members_required"] = "لازم تضيف عضو دافع واحد على الأقل.",
        ["association.periods_required"] = "لازم تكون كل فترات الجمعية جاهزة قبل التشغيل.",
        ["association.members_after_start"] = "لا يمكن إضافة عضو عادي بعد تشغيل الجمعية.",
        ["association.start_from_draft"] = "يمكن تشغيل الجمعية وهي مسودة فقط.",
        ["association.pause_from_active"] = "يمكن إيقاف الجمعية مؤقتًا وهي نشطة فقط.",
        ["association.resume_from_paused"] = "يمكن استكمال جمعية متوقفة مؤقتًا فقط.",
        ["association.complete_state"] = "لا يمكن إنهاء الجمعية في حالتها الحالية.",
        ["association.open_periods"] = "لازم تقفل كل الفترات قبل إنهاء الجمعية.",
        ["association.closed"] = "الجمعية المقفولة لا تقبل تعديلات جديدة.",
        ["person.not_found"] = "الشخص غير موجود.",
        ["person.duplicate_phone"] = "رقم الموبايل مسجل قبل كده.",
        ["person.duplicate_name"] = "في شخص مسجل بنفس الاسم.",
        ["person.name_required"] = "اسم الشخص مطلوب.",
        ["person.phone_required"] = "رقم الموبايل مطلوب.",
        ["person.invalid_phone"] = "رقم الموبايل غير صحيح.",
        ["person.invalid_subscriber_code"] = "كود المشترك غير صحيح.",
        ["person.required"] = "اختيار الشخص مطلوب.",
        ["membership.not_found"] = "العضوية غير موجودة.",
        ["membership.duplicate"] = "الشخص عضو نشط بالفعل في الجمعية.",
        ["membership.invalid_kind"] = "نوع العضوية غير صحيح.",
        ["membership.invalid_shares"] = "عدد الأسهم غير صحيح.",
        ["membership.shares_immutable"] = "لا يمكن تغيير الأسهم بعد تشغيل الجمعية.",
        ["membership.not_active"] = "لا يمكن استبدال عضوية غير نشطة.",
        ["period.not_found"] = "فترة الجمعية غير موجودة.",
        ["payout.not_found"] = "موعد القبض غير موجود.",
        ["payout.invalid_shares"] = "عدد أسهم القبض غير صحيح.",
        ["payout.invalid_recipient_type"] = "نوع مستلم القبض غير صحيح.",
        ["payout.membership_required"] = "لازم تحدد العضوية لموعد قبض العضو.",
        ["payout.reserve_full_share"] = "بدون اسم لازم يمثل سهمًا كاملًا واحدًا.",
        ["payout.reserve_cannot_be_person"] = "بدون اسم لا يمكن تسجيله كشخص.",
        ["payout.future_only"] = "يمكن تعديل مواعيد القبض المستقبلية فقط.",
        ["payout.historical_immutable"] = "لا يمكن تغيير قبض تاريخي.",
        ["payout.not_due"] = "لا يمكن تنفيذ قبض مستقبلي.",
        ["payout.invalid_association_state"] = "لا يمكن تنفيذ القبض في حالة الجمعية الحالية.",
        ["payout.already_completed"] = "تم تنفيذ القبض بالفعل.",
        ["payout.not_reserve"] = "يمكن ربط المستفيد بقبض بدون اسم فقط.",
        ["payout.reason_too_long"] = "سبب تعديل القبض أطول من المسموح.",
        ["payment.not_found"] = "الدفعة غير موجودة.",
        ["payment.no_accessible_associations"] = "لا توجد جمعيات متاحة لهذا الشخص.",
        ["payment.invalid_amount"] = "قيمة الدفعة غير صحيحة.",
        ["payment.invalid_method"] = "طريقة الدفع غير صحيحة.",
        ["payment.zero_amount"] = "قيمة الدفعة لا يمكن أن تكون صفرًا.",
        ["payment.credit_association_required"] = "اختار الجمعية اللي هيتسجل عليها الرصيد الزيادة.",
        ["payment.adjustment_reason"] = "سبب التصحيح مطلوب.",
        ["payment.adjustment_entries_required"] = "بنود التصحيح مطلوبة.",
        ["payment.duplicate_adjustment_entry"] = "لا يمكن تكرار نفس بند التصحيح.",
        ["payment.adjustment_total_mismatch"] = "إجمالي بنود التصحيح لازم يساوي قيمة التصحيح.",
        ["payment.adjustment_scope"] = "التصحيح يقتصر على بنود الدفعة الأصلية.",
        ["payment.adjustment_exceeds_original"] = "قيمة التصحيح أكبر من القيمة المتاحة في الدفعة الأصلية.",
        ["payment.credit_already_applied"] = "لا يمكن عكس رصيد تم استخدامه بالفعل.",
        ["replacement.required"] = "لازم تحدد شخصًا بديلًا واحدًا على الأقل.",
        ["replacement.duplicate_person"] = "لا يمكن تكرار نفس الشخص ضمن البدلاء.",
        ["replacement.share_mismatch"] = "إجمالي أسهم البدلاء لازم يساوي أسهم العضو القديم.",
        ["replacement.after_start"] = "الاستبدال متاح بعد تشغيل الجمعية فقط.",
        ["replacement.reason_too_long"] = "سبب الاستبدال أطول من المسموح.",
        ["report.invalid_period"] = "سنة أو شهر التقرير غير صحيح."
    };

    public static bool IsArabic(HttpRequest request) => request.GetTypedHeaders().AcceptLanguage?.Any(x =>
        x.Value.Value?.StartsWith("ar", StringComparison.OrdinalIgnoreCase) == true) == true;

    public static string Arabic(string code) => Ar.GetValueOrDefault(code, "تعذر تنفيذ الطلب.");
    public static string English(string code) => En.GetValueOrDefault(code, "The value is invalid.");
    public static string ArabicTitle(string code) => code switch
    {
        "validation.failed" => "راجع البيانات",
        "authentication.required" or "auth.invalid_credentials" or "auth.invalid_participant_credentials" => "تعذر تسجيل الدخول",
        "authorization.forbidden" => "غير مسموح",
        "server.error" => "خطأ غير متوقع",
        _ when code.EndsWith(".not_found", StringComparison.Ordinal) => "غير موجود",
        _ => "تعذر تنفيذ الطلب"
    };
}
