using System.Net;

namespace POS.Application.Notifications;

public record LowStockEmailData(
    string BranchName,
    string MaterialName,
    string Unit,
    decimal CurrentQuantity,
    decimal LowStockThreshold,
    DateTime TriggeredAt);

public static class LowStockEmailTemplate
{
    public static decimal SuggestedReplenishment(decimal current, decimal threshold) =>
        Math.Max(0m, threshold - current);

    public static string Build(LowStockEmailData data)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        var replenish = SuggestedReplenishment(data.CurrentQuantity, data.LowStockThreshold);
        var unit = string.IsNullOrWhiteSpace(data.Unit) ? "وحدة" : data.Unit;

        return $$"""
        <!doctype html>
        <html lang="ar" dir="rtl">
        <body style="margin:0;background:#f4f1ea;font-family:Tahoma,Arial,sans-serif;color:#29251f">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="padding:28px 12px;background:#f4f1ea">
            <tr><td align="center">
              <table role="presentation" width="620" cellspacing="0" cellpadding="0" style="max-width:620px;width:100%;background:#fff;border:1px solid #ded6c8;border-radius:16px;overflow:hidden">
                <tr><td style="padding:24px 28px;background:#7b2d26;color:#fff">
                  <div style="font-size:13px;opacity:.85">نظام لولاة — إشعار مخزون</div>
                  <div style="font-size:25px;font-weight:bold;margin-top:6px">تنبيه انخفاض المخزون</div>
                </td></tr>
                <tr><td style="padding:26px 28px">
                  <div style="font-size:14px;color:#72685d">الفرع</div>
                  <div style="font-size:20px;font-weight:bold;margin:4px 0 22px">{{E(data.BranchName)}}</div>
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse">
                    <tr style="background:#faf8f4"><td style="padding:13px;border:1px solid #e7e0d6">الصنف المطلوب</td><td style="padding:13px;border:1px solid #e7e0d6;font-weight:bold">{{E(data.MaterialName)}}</td></tr>
                    <tr><td style="padding:13px;border:1px solid #e7e0d6">الرصيد الحالي</td><td style="padding:13px;border:1px solid #e7e0d6;color:#b42318;font-weight:bold">{{data.CurrentQuantity:N3}} {{E(unit)}}</td></tr>
                    <tr style="background:#faf8f4"><td style="padding:13px;border:1px solid #e7e0d6">حد التنبيه</td><td style="padding:13px;border:1px solid #e7e0d6">{{data.LowStockThreshold:N3}} {{E(unit)}}</td></tr>
                    <tr><td style="padding:13px;border:1px solid #e7e0d6">الكمية المطلوبة للوصول إلى الحد</td><td style="padding:13px;border:1px solid #e7e0d6;color:#176b45;font-weight:bold">{{replenish:N3}} {{E(unit)}}</td></tr>
                  </table>
                  <div style="margin-top:22px;padding:14px 16px;background:#fff4e5;border-right:4px solid #f79009;border-radius:8px;font-size:14px">
                    البيع لم يتوقف. يرجى توفير الصنف للفرع في أقرب وقت.
                  </div>
                  <div style="margin-top:20px;font-size:12px;color:#8a8177">وقت التنبيه: {{data.TriggeredAt.ToString("yyyy-MM-dd HH:mm")}} UTC</div>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }
}
