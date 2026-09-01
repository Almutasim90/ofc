using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Printing;
using POS.Application.QrOrdering;
using POS.Application.RestaurantInventory;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Infrastructure.Persistence;
using Xunit;

namespace POS.Application.Tests;

public class QrOrderingTests
{
    private const string SigningSecret = "qr-test-secret-that-is-longer-than-thirty-two-bytes";

    [Fact]
    public void Signed_token_round_trips_and_rejects_a_different_point()
    {
        var tokens = new QrTokenService(SigningSecret);
        var pointId = Guid.NewGuid();
        var token = tokens.Generate(pointId, 1);

        Assert.True(tokens.Verify(pointId, 1, token));
        Assert.False(tokens.Verify(Guid.NewGuid(), 1, token));
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }

    [Fact]
    public async Task Anonymous_scan_bypasses_only_capability_rooted_branch_filters()
    {
        var anonymous = new AnonymousUser();
        await using var db = Db(anonymous);
        var point = Point(db, OrderingPointTypes.Table, "T1");

        var first = await Service(db, anonymous).ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);
        var second = await Service(db, anonymous).ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Empty(db.OrderingSessions);
        Assert.Single(db.OrderingSessions.IgnoreQueryFilters());
    }

    [Fact]
    public async Task Signed_scan_rejects_tampering_and_regeneration_revokes_all_old_links()
    {
        await using var db = Db();
        var point = Point(db, OrderingPointTypes.Table, "T1");
        var service = Service(db);
        var tokens = new QrTokenService(SigningSecret);
        var signed = tokens.Generate(point.Id, point.QrTokenVersion);
        var legacy = point.QrCodeToken;

        var session = await service.ResolveSignedAsync(point.Id, signed, TestContext.Current.CancellationToken);
        Assert.Equal(point.Id, session.PointId);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ResolveSignedAsync(point.Id, signed + "x", TestContext.Current.CancellationToken));

        db.OrderingSessions.Remove(db.OrderingSessions.IgnoreQueryFilters().Single());
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var regenerated = await service.RegenerateAsync(point.Id, TestContext.Current.CancellationToken);

        Assert.NotEqual(signed, regenerated);
        await Assert.ThrowsAsync<NotFoundException>(() => service.ResolveSignedAsync(point.Id, signed, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotFoundException>(() => service.ResolveAsync(legacy, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Car_bay_requires_enabled_branch_feature()
    {
        await using var db = Db();
        var point = Point(db, OrderingPointTypes.CarBay, "Bay 3");
        var service = Service(db);

        await Assert.ThrowsAsync<NotFoundException>(() => service.ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken));
        db.BranchFeatureFlags.Add(new() { Id = Guid.NewGuid(), BranchId = point.BranchId, FeatureKey = BranchFeatureKeys.CarPickup, IsEnabled = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);
        Assert.Equal(OrderingPointTypes.CarBay, result.PointType);
        Assert.Equal("Bay 3", result.Label);
    }

    [Fact]
    public async Task Additions_require_capability_and_use_point_channel_on_one_invoice()
    {
        await using var db = Db();
        var point = Point(db, OrderingPointTypes.Table, "T1");
        var service = Service(db);
        var session = await service.ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);
        var item = MenuItem(db);
        var channel = Channel(db, point.BranchId, "QR_TABLE");
        await SeedOpenOrder(db, point, session.SessionId, channel.Id);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var request = new AddQrOrderRequest(session.AccessToken, [new(item.Id, 1, null, [], [])]);

        await Assert.ThrowsAsync<NotFoundException>(() => service.AddAsync(session.SessionId, request with { AccessToken = "wrong" }, TestContext.Current.CancellationToken));
        await service.AddAsync(session.SessionId, request, TestContext.Current.CancellationToken);
        await service.AddAsync(session.SessionId, request, TestContext.Current.CancellationToken);

        var order = await db.RestaurantOrders.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(channel.Id, order.SalesChannelId);
        Assert.Equal(2, order.Items.Count);
        Assert.Equal(4, order.GrandTotal);
    }

    [Fact]
    public async Task Prepayment_rule_rejects_unpaid_confirmation()
    {
        await using var db = Db();
        var point = Point(db, OrderingPointTypes.Table, "T1");
        var session = await Service(db).ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);
        var channel = Channel(db, point.BranchId, "QR_TABLE", true);
        var order = new RestaurantOrder { Id = Guid.NewGuid(), BranchId = point.BranchId, OrderingSessionId = session.SessionId, SalesChannelId = channel.Id, GrandTotal = 5, Status = RestaurantOrderStatuses.Open };
        db.RestaurantOrders.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => Service(db).ConfirmAsync(order.Id, new(session.SessionId, session.AccessToken), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Simultaneous_phones_append_to_the_same_session_invoice()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
        Guid sessionId; Guid itemId; string token;
        await using (var seed = Db(options))
        {
            var point = Point(seed, OrderingPointTypes.Table, "T1");
            sessionId = (await Service(seed).ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken)).SessionId;
            token = (await seed.OrderingSessions.SingleAsync(TestContext.Current.CancellationToken)).AccessToken;
            itemId = MenuItem(seed).Id;
            var channel = Channel(seed, point.BranchId, "QR_TABLE");
            await SeedOpenOrder(seed, point, sessionId, channel.Id);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var request = new AddQrOrderRequest(token, [new(itemId, 1, null, [], [])]);
        await using var first = Db(options);
        await using var second = Db(options);

        await Task.WhenAll(Service(first).AddAsync(sessionId, request, TestContext.Current.CancellationToken), Service(second).AddAsync(sessionId, request, TestContext.Current.CancellationToken));

        await using var verify = Db(options);
        Assert.Single(verify.RestaurantOrders);
        Assert.Equal(2, verify.RestaurantOrderItems.Count());
        Assert.Equal(4, (await verify.RestaurantOrders.SingleAsync(TestContext.Current.CancellationToken)).GrandTotal);
    }

    [Fact]
    public async Task Fully_paid_qr_order_can_be_sent_and_cannot_spawn_another_invoice()
    {
        await using var db = Db();
        var point = Point(db, OrderingPointTypes.Table, "T1");
        var printer = new Printer();
        var service = Service(db, printer: printer);
        var session = await service.ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);
        var item = MenuItem(db);
        var channel = Channel(db, point.BranchId, "QR_TABLE", true);
        await SeedOpenOrder(db, point, session.SessionId, channel.Id);
        var paymentMethod = await db.PaymentMethods.SingleAsync(x => x.Code == "CARD", TestContext.Current.CancellationToken);
        db.Warehouses.Add(new() { Id = Guid.NewGuid(), BranchId = point.BranchId, NameAr = "مخزن", NameEn = "Warehouse", IsDefault = true });
        db.PrinterConfigs.Add(new() { Id = Guid.NewGuid(), BranchId = point.BranchId, IpAddress = "127.0.0.1", Port = 9100, IsDefault = true, IsActive = true });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var request = new AddQrOrderRequest(session.AccessToken, [new(item.Id, 1, null, [], [])]);
        var created = await service.AddAsync(session.SessionId, request, TestContext.Current.CancellationToken);
        var order = await db.RestaurantOrders.Include(x => x.Payments).SingleAsync(x => x.Id == created.Id, TestContext.Current.CancellationToken);
        order.Status = RestaurantOrderStatuses.Paid;
        var payment = new OrderPayment { Id = Guid.NewGuid(), OrderId = order.Id, PaymentMethodId = paymentMethod.Id, Amount = order.GrandTotal, CreatedAt = DateTime.UtcNow };
        order.Payments.Add(payment);
        db.OrderPayments.Add(payment);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await service.ConfirmAsync(order.Id, new(session.SessionId, session.AccessToken), TestContext.Current.CancellationToken);

        Assert.Equal(RestaurantOrderStatuses.Sent, order.Status);
        Assert.True(printer.SendCount >= 2);
        var printCount = printer.SendCount;
        await service.ConfirmAsync(order.Id, new(session.SessionId, session.AccessToken), TestContext.Current.CancellationToken);
        Assert.Equal(printCount, printer.SendCount);
        await Assert.ThrowsAsync<ValidationException>(() => service.AddAsync(session.SessionId, request, TestContext.Current.CancellationToken));
        Assert.Single(db.RestaurantOrders);
    }

    [Fact]
    public async Task Closing_settled_session_rotates_token()
    {
        await using var db = Db();
        var point = Point(db, OrderingPointTypes.Table, "T1");
        var service = Service(db);
        var session = await service.ResolveAsync(point.QrCodeToken, TestContext.Current.CancellationToken);
        db.RestaurantOrders.Add(new() { Id = Guid.NewGuid(), BranchId = point.BranchId, OrderingSessionId = session.SessionId, Status = RestaurantOrderStatuses.Paid });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var physicalToken = new QrTokenService(SigningSecret).Generate(point.Id, point.QrTokenVersion);

        await service.CloseAsync(session.SessionId, TestContext.Current.CancellationToken);

        Assert.True(new QrTokenService(SigningSecret).Verify(point.Id, point.QrTokenVersion, physicalToken));
        Assert.Equal(OrderingSessionStatuses.Closed, db.OrderingSessions.Single().Status);
    }

    private static AppDbContext Db(ICurrentUserService? user = null)
    {
        return Db(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, user);
    }

    private static AppDbContext Db(DbContextOptions<AppDbContext> options, ICurrentUserService? user = null)
    {
        var db = new AppDbContext(options, user);
        db.Database.EnsureCreated();
        return db;
    }

    private static QrOrderingService Service(AppDbContext db, ICurrentUserService? user = null, Printer? printer = null)
    {
        user ??= new User();
        return new(db, new OrderPrintingService(db, printer ?? new Printer(), new RestaurantInventoryService(db, user)), user, new QrTokenService(SigningSecret));
    }

    private static OrderingPoint Point(AppDbContext db, string type, string label)
    {
        var branch = new Branch { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString("N"), NameAr = "فرع", NameEn = "Branch", IsActive = true };
        var point = new OrderingPoint { Id = Guid.NewGuid(), Branch = branch, BranchId = branch.Id, PointType = type, QrCodeToken = Guid.NewGuid().ToString("N"), IsActive = true };
        if (type == OrderingPointTypes.Table)
        {
            var table = new RestaurantTable { Id = Guid.NewGuid(), Branch = branch, BranchId = branch.Id, Label = label, IsActive = true };
            point.LinkedTable = table; point.LinkedTableId = table.Id;
        }
        else
        {
            var bay = new CarPickupBay { Id = Guid.NewGuid(), Branch = branch, BranchId = branch.Id, BayLabel = label, IsActive = true };
            point.LinkedCarBay = bay; point.LinkedCarBayId = bay.Id;
        }
        db.OrderingPoints.Add(point); db.SaveChanges(); return point;
    }

    private static MenuItem MenuItem(AppDbContext db)
    {
        var category = new MenuCategory { Id = Guid.NewGuid(), NameAr = "فئة", NameEn = "Category" };
        var item = new MenuItem { Id = Guid.NewGuid(), Category = category, NameAr = "صنف", NameEn = "Item", BasePrice = 2 };
        db.AddRange(category, item); return item;
    }

    private static SalesChannel Channel(AppDbContext db, Guid branchId, string code, bool prepayment = false)
    {
        var channel = new SalesChannel { Id = Guid.NewGuid(), Code = code, NameAr = "QR", NameEn = "QR", IsActive = true };
        db.SalesChannels.Add(channel);
        db.BranchSalesChannelAvailabilities.Add(new() { Id = Guid.NewGuid(), BranchId = branchId, SalesChannel = channel, IsEnabled = true, RequiresPrepayment = prepayment });
        return channel;
    }

    private static async Task SeedOpenOrder(AppDbContext db, OrderingPoint point, Guid sessionId, Guid channelId)
    {
        var type = await db.OrderTypes.SingleAsync(x => x.Code == "DINE_IN", TestContext.Current.CancellationToken);
        db.RestaurantOrders.Add(new() { Id = Guid.NewGuid(), BranchId = point.BranchId, OrderNumber = 1, OrderTypeId = type.Id, TableId = point.LinkedTableId, SalesChannelId = channelId, OrderingSessionId = sessionId, Status = RestaurantOrderStatuses.Open });
    }

    private sealed class Printer : IRawPrinterClient
    {
        public int SendCount { get; private set; }
        public Task SendAsync(string ipAddress, int port, byte[] payload, CancellationToken cancellationToken = default) { SendCount++; return Task.CompletedTask; }
    }

    private sealed class User : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid(); public Guid? BranchId => null; public string? RoleName => null;
        public IReadOnlyCollection<string> Permissions => []; public bool BypassBranchFilter => true;
    }

    private sealed class AnonymousUser : ICurrentUserService
    {
        public Guid? UserId => null; public Guid? BranchId => null; public string? RoleName => null;
        public IReadOnlyCollection<string> Permissions => []; public bool BypassBranchFilter => false;
    }
}
