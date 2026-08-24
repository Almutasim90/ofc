using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Domain.Entities;

namespace POS.Application.Channels;

public class ChannelService(IAppDbContext db)
{
    public async Task<List<SalesChannelDto>> GetAllAsync(bool activeOnly, CancellationToken ct = default) =>
        await db.SalesChannels.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderByDescending(c => c.IsInStore)
            .Select(c => new SalesChannelDto(c.Id, c.NameAr, c.NameEn, c.LogoUrl, c.IsActive, c.IsInStore)).ToListAsync(ct);
    public async Task<SalesChannelDto> CreateAsync(UpsertSalesChannelRequest r, CancellationToken ct = default)
    {
        var c = new SalesChannel { Id = Guid.NewGuid(), NameAr = r.NameAr.Trim(), NameEn = r.NameEn.Trim(), LogoUrl = r.LogoUrl, IsActive = r.IsActive };
        db.SalesChannels.Add(c); await db.SaveChangesAsync(ct); return ToDto(c);
    }
    public async Task<SalesChannelDto> UpdateAsync(Guid id, UpsertSalesChannelRequest r, CancellationToken ct = default)
    {
        var c = await db.SalesChannels.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Channel not found.");
        c.NameAr = r.NameAr.Trim(); c.NameEn = r.NameEn.Trim(); c.LogoUrl = r.LogoUrl;
        if (!c.IsInStore) c.IsActive = r.IsActive;
        await db.SaveChangesAsync(ct); return ToDto(c);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var c = await db.SalesChannels.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Channel not found.");
        if (c.IsInStore) throw new ValidationException("The in-store channel cannot be deleted.");
        if (await db.Sales.AnyAsync(s => s.ChannelId == id, ct)) c.IsActive = false;
        else db.SalesChannels.Remove(c);
        await db.SaveChangesAsync(ct);
    }
    public async Task<List<ProductChannelPriceDto>> GetPricesAsync(Guid id, CancellationToken ct = default) =>
        await db.Products.AsNoTracking().OrderBy(p => p.NameEn).Select(p => new ProductChannelPriceDto(p.Id,
            db.ProductChannelPrices.Where(x => x.ChannelId == id && x.ProductId == p.Id).Select(x => (decimal?)x.Price).FirstOrDefault())).ToListAsync(ct);
    public async Task SetPricesAsync(Guid id, SetChannelPricesRequest request, CancellationToken ct = default)
    {
        var existing = await db.ProductChannelPrices.Where(x => x.ChannelId == id).ToListAsync(ct);
        db.ProductChannelPrices.RemoveRange(existing);
        foreach (var p in request.Prices.Where(p => p.Price.HasValue)) db.ProductChannelPrices.Add(new ProductChannelPrice { ChannelId = id, ProductId = p.ProductId, Price = p.Price!.Value });
        await db.SaveChangesAsync(ct);
    }
    private static SalesChannelDto ToDto(SalesChannel c) => new(c.Id, c.NameAr, c.NameEn, c.LogoUrl, c.IsActive, c.IsInStore);
}
