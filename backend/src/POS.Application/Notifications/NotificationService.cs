using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;

namespace POS.Application.Notifications;

public record LowStockNotificationDto(Guid Id, Guid BranchId, string BranchNameAr, string BranchNameEn,
    Guid RawMaterialId, string MaterialNameAr, string MaterialNameEn, DateTime TriggeredAt, DateTime? ResolvedAt);

public class NotificationService(IAppDbContext db)
{
    public async Task<List<LowStockNotificationDto>> GetAsync(bool includeResolved, CancellationToken ct = default) =>
        await db.LowStockNotifications.AsNoTracking().Where(n => includeResolved || n.ResolvedAt == null)
            .OrderByDescending(n => n.TriggeredAt).Select(n => new LowStockNotificationDto(n.Id, n.BranchId,
                n.Branch.NameAr, n.Branch.NameEn, n.RawMaterialId, n.RawMaterial.NameAr, n.RawMaterial.NameEn,
                n.TriggeredAt, n.ResolvedAt)).ToListAsync(ct);
}
