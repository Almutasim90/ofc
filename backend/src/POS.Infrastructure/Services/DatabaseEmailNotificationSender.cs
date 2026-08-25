using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Infrastructure.Persistence;

namespace POS.Infrastructure.Services;

public class DatabaseEmailNotificationSender(AppDbContext db, IDataProtectionProvider protection) : IEmailNotificationSender
{
    private readonly IDataProtector protector = protection.CreateProtector("POS.Email.Password.v1");

    public async Task SendAsync(string subject, string body, string? recipientOverride = null, bool isHtml = false, CancellationToken cancellationToken = default)
    {
        var settings = await db.EmailSettings.AsNoTracking().SingleOrDefaultAsync(cancellationToken)
            ?? throw new ValidationException("Email settings have not been configured.");
        if (!settings.IsActive) throw new ValidationException("Email notifications are disabled.");

        string password;
        try { password = protector.Unprotect(settings.PasswordEncrypted); }
        catch (System.Security.Cryptography.CryptographicException)
        {
            throw new ValidationException("The saved SMTP password can no longer be decrypted. Enter it again and save the settings.");
        }
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
        {
            EnableSsl = settings.UseSsl,
            Credentials = new NetworkCredential(settings.Username, password),
            Timeout = 15000,
        };
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = isHtml,
        };
        foreach (var recipient in (recipientOverride ?? settings.Recipients).Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(recipient);
        try
        {
            await client.SendMailAsync(message, cancellationToken);
        }
        catch (SmtpException ex)
        {
            throw new ValidationException($"SMTP delivery failed: {ex.Message}. Verify the server, port, SSL, and app password.");
        }
    }
}
