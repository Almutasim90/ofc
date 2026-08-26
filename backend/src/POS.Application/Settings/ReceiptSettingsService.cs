using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Settings;

public record ReceiptSettingsDto(string? HeaderText);
public record UpdateReceiptSettingsRequest(string? HeaderText);

public class ReceiptSettingsService(IAppDbContext db)
{
    public async Task<ReceiptSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var settings = await db.ReceiptSettings.AsNoTracking().SingleOrDefaultAsync(ct);
        return new(settings?.HeaderText);
    }

    public async Task<ReceiptSettingsDto> SaveAsync(UpdateReceiptSettingsRequest request, CancellationToken ct = default)
    {
        var headerText = request.HeaderText?.Trim();
        if (headerText?.Length > 500) throw new ValidationException("The receipt header can be at most 500 characters.");
        var settings = await db.ReceiptSettings.SingleOrDefaultAsync(ct);
        if (settings is null)
        {
            settings = new ReceiptSettings { Id = Guid.NewGuid() };
            db.ReceiptSettings.Add(settings);
        }
        settings.HeaderText = string.IsNullOrWhiteSpace(headerText) ? null : headerText;
        await db.SaveChangesAsync(ct);
        return new(settings.HeaderText);
    }
}
