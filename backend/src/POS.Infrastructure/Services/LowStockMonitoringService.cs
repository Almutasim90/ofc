using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Infrastructure.Persistence;
using POS.Domain.Entities;

namespace POS.Infrastructure.Services;

public class LowStockMonitoringService(IServiceScopeFactory scopes, ILogger<LowStockMonitoringService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await CheckAsync(stoppingToken); } catch (Exception ex) { logger.LogError(ex, "Low-stock check failed."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
    private async Task CheckAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stocks = await db.BranchRawMaterialStocks.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var open = await db.LowStockNotifications.IgnoreQueryFilters().Where(n => n.ResolvedAt == null).ToListAsync(ct);
        foreach (var stock in stocks)
        {
            var existing = open.FirstOrDefault(n => n.BranchId == stock.BranchId && n.RawMaterialId == stock.RawMaterialId);
            if (stock.CurrentQuantity <= stock.LowStockThreshold && existing is null)
            {
                db.LowStockNotifications.Add(new LowStockNotification { Id = Guid.NewGuid(), BranchId = stock.BranchId, RawMaterialId = stock.RawMaterialId, TriggeredAt = DateTime.UtcNow });
                await TrySendEmailAsync(stock.BranchId, stock.RawMaterialId, ct);
            }
            else if (stock.CurrentQuantity > stock.LowStockThreshold && existing is not null) existing.ResolvedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
    private async Task TrySendEmailAsync(Guid branchId, Guid materialId, CancellationToken ct)
    {
        var host = Environment.GetEnvironmentVariable("SMTP_HOST");
        var recipients = Environment.GetEnvironmentVariable("SMTP_ALERT_RECIPIENTS");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(recipients)) return;
        using var client = new SmtpClient(host, int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var port) ? port : 587)
        { EnableSsl = true, Credentials = new NetworkCredential(Environment.GetEnvironmentVariable("SMTP_USERNAME"), Environment.GetEnvironmentVariable("SMTP_PASSWORD")) };
        using var message = new MailMessage(Environment.GetEnvironmentVariable("SMTP_FROM") ?? "alerts@localhost", recipients)
        { Subject = "POS low stock alert", Body = $"Low stock detected. Branch: {branchId}; material: {materialId}." };
        await client.SendMailAsync(message, ct);
    }
}
