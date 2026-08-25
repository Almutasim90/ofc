using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Settings;

public record EmailSettingsDto(Guid? Id, string SmtpHost, int SmtpPort, bool UseSsl, string Username,
    bool HasPassword, string FromEmail, string FromName, string Recipients, bool IsActive);
public record UpdateEmailSettingsRequest(string SmtpHost, int SmtpPort, bool UseSsl, string Username,
    string? Password, string FromEmail, string FromName, string Recipients, bool IsActive);
public record TestEmailRequest(string Recipient);

public class EmailSettingsService(IAppDbContext db, IDataProtectionProvider protection, IEmailNotificationSender sender)
{
    private readonly IDataProtector protector = protection.CreateProtector("POS.Email.Password.v1");

    public async Task<EmailSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(ct);
        return settings is null
            ? new(null, "", 587, true, "", false, "", "", "", false)
            : ToDto(settings);
    }

    public async Task<EmailSettingsDto> SaveAsync(UpdateEmailSettingsRequest request, CancellationToken ct = default)
    {
        Validate(request);
        var settings = await db.EmailSettings.SingleOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new EmailSettings { Id = Guid.NewGuid() };
            db.EmailSettings.Add(settings);
        }
        settings.SmtpHost = request.SmtpHost.Trim();
        settings.SmtpPort = request.SmtpPort;
        settings.UseSsl = request.UseSsl;
        settings.Username = request.Username.Trim();
        settings.FromEmail = request.FromEmail.Trim();
        settings.FromName = request.FromName.Trim();
        settings.Recipients = NormalizeRecipients(request.Recipients);
        settings.IsActive = request.IsActive;
        if (!string.IsNullOrWhiteSpace(request.Password)) settings.PasswordEncrypted = protector.Protect(request.Password);
        if (settings.IsActive && string.IsNullOrWhiteSpace(settings.PasswordEncrypted))
            throw new ValidationException("SMTP password is required when email notifications are active.");
        await db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    public async Task SendTestAsync(string recipient, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recipient) || !IsValidEmail(recipient))
            throw new ValidationException("Enter a valid test email address.");
        var sentAt = POS.Application.Closing.MuscatClock.ToLocal(DateTime.UtcNow);
        var html = $$"""
            <!doctype html>
            <html lang="ar">
            <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
            <body style="margin:0;padding:24px;font-family:Tahoma,Arial,sans-serif;line-height:1.7">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;margin:0 auto;border:1px solid;border-radius:14px;overflow:hidden">
                <tr><td style="padding:28px;text-align:center;border-bottom:1px solid">
                  <div style="font-size:28px;font-weight:700">لولاة السويق</div>
                  <div style="font-size:14px">Lolat Al Suwaiq POS</div>
                </td></tr>
                <tr><td style="padding:28px">
                  <section dir="rtl" style="text-align:right">
                    <h1 style="margin:0 0 10px;font-size:22px">تم إعداد البريد الإلكتروني بنجاح</h1>
                    <p style="margin:0">هذه رسالة اختبار للتأكد من أن نظام نقاط البيع يستطيع إرسال التنبيهات التلقائية بصورة صحيحة.</p>
                  </section>
                  <hr style="margin:24px 0;border:0;border-top:1px solid">
                  <section dir="ltr" style="text-align:left">
                    <h2 style="margin:0 0 10px;font-size:20px">Email notifications are ready</h2>
                    <p style="margin:0">This test confirms that the point-of-sale system can deliver automatic notifications successfully.</p>
                  </section>
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="10" style="margin-top:24px;border:1px solid;border-radius:10px">
                    <tr><td dir="rtl" style="text-align:right"><strong>الحالة / Status</strong></td><td style="text-align:left">Connected</td></tr>
                    <tr><td dir="rtl" style="text-align:right"><strong>وقت الإرسال / Sent at</strong></td><td style="text-align:left">{{sentAt:yyyy-MM-dd HH:mm}} Asia/Muscat</td></tr>
                  </table>
                </td></tr>
                <tr><td dir="rtl" style="padding:18px;text-align:center;border-top:1px solid;font-size:12px">
                  رسالة آلية من نظام لولاة السويق · Automated message from Lolat Al Suwaiq POS
                </td></tr>
              </table>
            </body>
            </html>
            """;
        await sender.SendAsync("اختبار تنبيهات لولاة السويق | Lolat POS notification test", html, recipient.Trim(), isHtml: true, cancellationToken: ct);
    }

    private static void Validate(UpdateEmailSettingsRequest request)
    {
        if (request.SmtpPort is < 1 or > 65535) throw new ValidationException("SMTP port must be between 1 and 65535.");
        if (!request.IsActive) return;
        if (string.IsNullOrWhiteSpace(request.SmtpHost) || string.IsNullOrWhiteSpace(request.FromEmail))
            throw new ValidationException("SMTP host and sender email are required.");
        if (!IsValidEmail(request.FromEmail)) throw new ValidationException("Enter a valid sender email address.");
        if (string.IsNullOrWhiteSpace(NormalizeRecipients(request.Recipients)))
            throw new ValidationException("At least one notification recipient is required.");
    }

    private static string NormalizeRecipients(string recipients)
    {
        var values = recipients.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Any(value => !IsValidEmail(value))) throw new ValidationException("One or more notification recipients are invalid.");
        return string.Join(';', values.Select(value => new System.Net.Mail.MailAddress(value).Address).Distinct(StringComparer.OrdinalIgnoreCase));
    }
    private static bool IsValidEmail(string value)
    {
        try { return new System.Net.Mail.MailAddress(value).Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase); }
        catch (FormatException) { return false; }
    }
    private static EmailSettingsDto ToDto(EmailSettings x) => new(x.Id, x.SmtpHost, x.SmtpPort, x.UseSsl,
        x.Username, !string.IsNullOrWhiteSpace(x.PasswordEncrypted), x.FromEmail, x.FromName, x.Recipients, x.IsActive);
}
