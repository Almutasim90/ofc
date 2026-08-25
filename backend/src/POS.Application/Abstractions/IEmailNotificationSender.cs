namespace POS.Application.Abstractions;

public interface IEmailNotificationSender
{
    Task SendAsync(string subject, string body, string? recipientOverride = null, bool isHtml = false, CancellationToken cancellationToken = default);
}
