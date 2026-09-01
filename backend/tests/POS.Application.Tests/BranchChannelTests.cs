using Microsoft.EntityFrameworkCore;
using POS.Application.Channels;
using POS.Application.Common;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class BranchChannelTests
{
    [Fact]
    public async Task Channel_can_have_different_settings_per_branch()
    {
        await using var db = Db();
        var a = Branch("A"); var b = Branch("B"); db.Branches.AddRange(a, b);
        var service = new ChannelService(db);
        var channel = await service.CreateAsync(new("QR_CAR", "سيارة QR", "QR car", null, true), TestContext.Current.CancellationToken);

        var first = await service.SetAvailabilityAsync(a.Id, channel.Id, new(true, true), TestContext.Current.CancellationToken);
        var second = await service.SetAvailabilityAsync(b.Id, channel.Id, new(false, false), TestContext.Current.CancellationToken);

        Assert.True(first.IsEnabled); Assert.True(first.RequiresPrepayment);
        Assert.False(second.IsEnabled); Assert.False(second.RequiresPrepayment);
    }

    [Fact]
    public async Task Missing_branch_setting_is_available_by_default()
    {
        await using var db = Db();
        var branch = Branch("A"); db.Branches.Add(branch);
        var service = new ChannelService(db);
        var channel = await service.CreateAsync(new("DELIVERY", "توصيل", "Delivery", null, true), TestContext.Current.CancellationToken);

        var setting = Assert.Single(await service.GetAvailabilityAsync(branch.Id, TestContext.Current.CancellationToken), x => x.SalesChannelId == channel.Id);

        Assert.True(setting.IsEnabled);
        Assert.False(setting.RequiresPrepayment);
    }

    [Fact]
    public async Task Channel_referenced_by_restaurant_order_is_deactivated_not_deleted()
    {
        await using var db = Db();
        var service = new ChannelService(db);
        var channel = await service.CreateAsync(new("QR_TABLE", "طاولة", "QR table", null, true), TestContext.Current.CancellationToken);
        var branch = Branch("A");
        var type = await db.OrderTypes.FirstAsync(TestContext.Current.CancellationToken);
        db.Branches.Add(branch);
        db.RestaurantOrders.Add(new() { Id = Guid.NewGuid(), Branch = branch, OrderType = type, SalesChannelId = channel.Id });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await service.DeleteAsync(channel.Id, TestContext.Current.CancellationToken);

        Assert.False(db.SalesChannels.Single(x => x.Id == channel.Id).IsActive);
    }

    [Fact]
    public async Task Channel_requires_bilingual_names()
    {
        await using var db = Db();
        await Assert.ThrowsAsync<ValidationException>(() => new ChannelService(db)
            .CreateAsync(new("APP", "", "App", null, true), TestContext.Current.CancellationToken));
    }

    private static AppDbContext Db()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Database.EnsureCreated(); return db;
    }

    private static Branch Branch(string code) => new() { Id = Guid.NewGuid(), Code = code, NameAr = code, NameEn = code };
}
