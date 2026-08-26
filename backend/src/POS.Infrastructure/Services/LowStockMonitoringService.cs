using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using POS.Infrastructure.Persistence;
using POS.Domain.Entities;
using POS.Application.Abstractions;
using POS.Application.Notifications;

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
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailNotificationSender>();
        var stocks = await db.BranchRawMaterialStocks.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
        var open = await db.LowStockNotifications.IgnoreQueryFilters().Where(n => n.ResolvedAt == null).ToListAsync(ct);
        foreach (var stock in stocks)
        {
            var existing = open.FirstOrDefault(n => n.BranchId == stock.BranchId && n.RawMaterialId == stock.RawMaterialId);
            if (stock.CurrentQuantity <= stock.LowStockThreshold && existing is null)
            {
                db.LowStockNotifications.Add(new LowStockNotification { Id = Guid.NewGuid(), BranchId = stock.BranchId, RawMaterialId = stock.RawMaterialId, TriggeredAt = DateTime.UtcNow });
                var branch = await db.Branches.IgnoreQueryFilters().Where(x => x.Id == stock.BranchId)
                    .Select(x => new { x.NameAr, x.NameEn }).SingleAsync(ct);
                var material = await db.RawMaterials.Where(x => x.Id == stock.RawMaterialId)
                    .Select(x => new { x.NameAr, x.NameEn, x.Unit }).SingleAsync(ct);
                try
                {
                    var body = LowStockEmailTemplate.Build(new LowStockEmailData(
                        branch.NameAr, branch.NameEn, material.NameAr, material.NameEn,
                        material.Unit, stock.CurrentQuantity,
                        stock.LowStockThreshold, DateTime.UtcNow));
                    await emailSender.SendAsync(
                        $"تنبيه انخفاض المخزون | Low Stock Alert: {material.NameAr} — {branch.NameAr}", body,
                        isHtml: true, cancellationToken: ct);
                }
                catch (Exception ex) { logger.LogWarning(ex, "Low-stock email could not be sent for branch {BranchId} material {MaterialId}.", stock.BranchId, stock.RawMaterialId); }
            }
            else if (stock.CurrentQuantity > stock.LowStockThreshold && existing is not null) existing.ResolvedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync(ct);
    }
}
