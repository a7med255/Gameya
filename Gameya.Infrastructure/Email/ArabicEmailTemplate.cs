using System.Text.Encodings.Web;

namespace Gameya.Infrastructure.Email;

internal static class ArabicEmailTemplate
{
    public static string Action(string title, string message, string actionLabel, string url,
        string footer)
    {
        var safeTitle = HtmlEncoder.Default.Encode(title);
        var safeMessage = HtmlEncoder.Default.Encode(message);
        var safeLabel = HtmlEncoder.Default.Encode(actionLabel);
        var safeUrl = HtmlEncoder.Default.Encode(url);
        var safeFooter = HtmlEncoder.Default.Encode(footer);
        return $$"""
            <!doctype html>
            <html lang="ar" dir="rtl">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#f3f7f6;font-family:Tahoma,Arial,sans-serif;color:#17342f;direction:rtl;text-align:right">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f7f6;padding:24px 12px">
                <tr><td align="center">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#fff;border-radius:16px;overflow:hidden;border:1px solid #dce8e5">
                    <tr><td style="background:#176b5b;color:#fff;padding:22px 28px;font-size:22px;font-weight:700">نظام الجمعية</td></tr>
                    <tr><td style="padding:30px 28px">
                      <h1 style="margin:0 0 16px;font-size:24px;line-height:1.5;color:#17342f">{{safeTitle}}</h1>
                      <p style="margin:0 0 24px;font-size:16px;line-height:1.9;color:#405b56">{{safeMessage}}</p>
                      <p style="margin:0 0 24px;text-align:center"><a href="{{safeUrl}}" style="display:inline-block;background:#176b5b;color:#fff;text-decoration:none;padding:13px 24px;border-radius:10px;font-size:16px;font-weight:700">{{safeLabel}}</a></p>
                      <p style="margin:0;font-size:13px;line-height:1.8;color:#6b7f7b">{{safeFooter}}</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string Report(int year, int month) => $$"""
        <!doctype html>
        <html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;background:#f3f7f6;font-family:Tahoma,Arial,sans-serif;color:#17342f;direction:rtl;text-align:right">
          <div style="max-width:600px;margin:24px auto;background:#fff;border:1px solid #dce8e5;border-radius:16px;padding:28px">
            <h1 style="font-size:24px;margin:0 0 14px">تقرير الجمعية الشهري</h1>
            <p style="font-size:16px;line-height:1.9;margin:0">مرفق تقرير شهر {{month:D2}} لسنة {{year:D4}} بصيغة PDF، ويشمل ملخص المستحق والمدفوع والمتبقي للجمعيات المتاحة لحسابك.</p>
          </div>
        </body></html>
        """;

    public static string AssociationReport(string associationName)
    {
        var safeName = HtmlEncoder.Default.Encode(associationName);
        return $$"""
            <!doctype html>
            <html lang="ar" dir="rtl"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;background:#f3f7f6;font-family:Tahoma,Arial,sans-serif;color:#17342f;direction:rtl;text-align:right">
              <div style="max-width:600px;margin:24px auto;background:#fff;border:1px solid #dce8e5;border-radius:16px;padding:28px">
                <h1 style="font-size:24px;margin:0 0 14px">تقرير الجمعية</h1>
                <p style="font-size:16px;line-height:1.9;margin:0">مرفق تقرير جمعية <strong>{{safeName}}</strong> بصيغة PDF، ويتضمن جدول الفترات والمستفيدين والمدفوعات.</p>
              </div>
            </body></html>
            """;
    }
}
