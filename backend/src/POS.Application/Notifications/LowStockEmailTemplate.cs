using System.Net;

namespace POS.Application.Notifications;

public record LowStockEmailData(string BranchNameAr, string BranchNameEn, string MaterialNameAr,
    string MaterialNameEn, string Unit, decimal CurrentQuantity, decimal LowStockThreshold, DateTime TriggeredAt);

public static class LowStockEmailTemplate
{
    public static decimal SuggestedReplenishment(decimal current, decimal threshold) => Math.Max(0m, threshold - current);

    public static string Build(LowStockEmailData data)
    {
        static string E(string value) => WebUtility.HtmlEncode(value);
        static string Name(string preferred, string fallback) => E(string.IsNullOrWhiteSpace(preferred) ? fallback : preferred);
        var replenish = SuggestedReplenishment(data.CurrentQuantity, data.LowStockThreshold);
        var unit = E(string.IsNullOrWhiteSpace(data.Unit) ? "unit" : data.Unit);
        var branchAr = Name(data.BranchNameAr, data.BranchNameEn);
        var branchEn = Name(data.BranchNameEn, data.BranchNameAr);
        var materialAr = Name(data.MaterialNameAr, data.MaterialNameEn);
        var materialEn = Name(data.MaterialNameEn, data.MaterialNameAr);
        var triggeredAt = data.TriggeredAt.ToString("yyyy-MM-dd HH:mm");

        return $$"""
        <!doctype html>
        <html lang="ar">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>تنبيه انخفاض المخزون | Low Stock Alert</title></head>
        <body style="margin:0;padding:0;background:#f3f0ea;font-family:Tahoma,Arial,sans-serif;color:#29251f">
          <div style="display:none;max-height:0;overflow:hidden;opacity:0">تنبيه آلي يتطلب مراجعة المخزون · Automated inventory alert requiring attention</div>
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;background:#f3f0ea"><tr><td align="center" style="padding:32px 12px">
            <table role="presentation" width="640" cellspacing="0" cellpadding="0" style="width:100%;max-width:640px;background:#fff;border:1px solid #ded6c8;border-radius:18px;overflow:hidden">
              <tr><td style="padding:26px 30px;background:#762b25;color:#fff;text-align:center">
                <div style="font-size:13px;letter-spacing:.4px;opacity:.86">LOLAT AL SUWAIQ · لولاة السويق</div>
                <div dir="rtl" style="font-size:25px;font-weight:700;margin-top:8px">تنبيه انخفاض المخزون</div>
                <div dir="ltr" style="font-size:16px;margin-top:4px;opacity:.92">Low Stock Alert</div>
              </td></tr>
              <tr><td style="padding:18px 30px;background:#fff8ed;border-bottom:1px solid #eadfce;text-align:center">
                <span style="display:inline-block;padding:6px 14px;border-radius:999px;background:#f79009;color:#fff;font-size:12px;font-weight:700">ACTION REQUIRED · يتطلب إجراء</span>
              </td></tr>
              <tr><td dir="rtl" align="right" style="padding:28px 30px 24px;text-align:right">
                <div style="font-size:20px;font-weight:700;color:#4b211d">السادة فريق العمليات المحترمين،</div>
                <p style="margin:10px 0 22px;font-size:14px;line-height:1.9;color:#61584f">نفيدكم بأن رصيد الصنف الموضح أدناه بلغ حد التنبيه. يرجى مراجعة الاحتياج واتخاذ الإجراء المناسب لضمان استمرارية التوفر.</p>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;border-collapse:collapse;font-size:14px">
                  <tr style="background:#faf8f4"><td width="46%" style="padding:13px;border:1px solid #e7e0d6;color:#6d6359">الفرع</td><td style="padding:13px;border:1px solid #e7e0d6;font-weight:700">{{branchAr}}</td></tr>
                  <tr><td style="padding:13px;border:1px solid #e7e0d6;color:#6d6359">الصنف</td><td style="padding:13px;border:1px solid #e7e0d6;font-weight:700">{{materialAr}}</td></tr>
                  <tr style="background:#fff5f3"><td style="padding:13px;border:1px solid #ead8d5;color:#6d6359">الرصيد الحالي</td><td dir="ltr" align="right" style="padding:13px;border:1px solid #ead8d5;color:#b42318;font-weight:700">{{data.CurrentQuantity:N3}} {{unit}}</td></tr>
                  <tr><td style="padding:13px;border:1px solid #e7e0d6;color:#6d6359">حد التنبيه</td><td dir="ltr" align="right" style="padding:13px;border:1px solid #e7e0d6">{{data.LowStockThreshold:N3}} {{unit}}</td></tr>
                  <tr style="background:#f0f8f4"><td style="padding:13px;border:1px solid #d7e9df;color:#6d6359">الكمية المقترحة للوصول إلى الحد</td><td dir="ltr" align="right" style="padding:13px;border:1px solid #d7e9df;color:#176b45;font-weight:700">{{replenish:N3}} {{unit}}</td></tr>
                </table>
                <p style="margin:18px 0 0;padding:14px 16px;background:#fff8ed;border-right:4px solid #f79009;border-radius:8px;font-size:13px;line-height:1.8">هذا إشعار آلي للمراجعة؛ لا يؤدي انخفاض المخزون إلى إيقاف عمليات البيع.</p>
              </td></tr>
              <tr><td style="padding:0 30px"><div style="height:1px;background:#e7e0d6"></div></td></tr>
              <tr><td dir="ltr" align="left" style="padding:24px 30px 28px;text-align:left">
                <div style="font-size:20px;font-weight:700;color:#4b211d">Dear Operations Team,</div>
                <p style="margin:10px 0 22px;font-size:14px;line-height:1.75;color:#61584f">Please be advised that the item below has reached its low-stock threshold. Kindly review the requirement and take the appropriate action to maintain availability.</p>
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="width:100%;border-collapse:collapse;font-size:14px">
                  <tr style="background:#faf8f4"><td width="46%" style="padding:13px;border:1px solid #e7e0d6;color:#6d6359">Branch</td><td style="padding:13px;border:1px solid #e7e0d6;font-weight:700">{{branchEn}}</td></tr>
                  <tr><td style="padding:13px;border:1px solid #e7e0d6;color:#6d6359">Item</td><td style="padding:13px;border:1px solid #e7e0d6;font-weight:700">{{materialEn}}</td></tr>
                  <tr style="background:#fff5f3"><td style="padding:13px;border:1px solid #ead8d5;color:#6d6359">Current balance</td><td style="padding:13px;border:1px solid #ead8d5;color:#b42318;font-weight:700">{{data.CurrentQuantity:N3}} {{unit}}</td></tr>
                  <tr><td style="padding:13px;border:1px solid #e7e0d6;color:#6d6359">Alert threshold</td><td style="padding:13px;border:1px solid #e7e0d6">{{data.LowStockThreshold:N3}} {{unit}}</td></tr>
                  <tr style="background:#f0f8f4"><td style="padding:13px;border:1px solid #d7e9df;color:#6d6359">Suggested quantity to reach threshold</td><td style="padding:13px;border:1px solid #d7e9df;color:#176b45;font-weight:700">{{replenish:N3}} {{unit}}</td></tr>
                </table>
                <p style="margin:18px 0 0;padding:14px 16px;background:#fff8ed;border-left:4px solid #f79009;border-radius:8px;font-size:13px;line-height:1.7">This automated notice is provided for review. Low stock does not suspend sales operations.</p>
              </td></tr>
              <tr><td style="padding:18px 30px;background:#29251f;color:#dcd5ca;text-align:center;font-size:11px;line-height:1.8">
                <div>وقت التنبيه · Alert time: {{triggeredAt}} UTC</div>
                <div>رسالة آلية من نظام لولاة السويق · Automated message from Lolat Al Suwaiq POS</div>
              </td></tr>
            </table>
          </td></tr></table>
        </body></html>
        """;
    }
}
