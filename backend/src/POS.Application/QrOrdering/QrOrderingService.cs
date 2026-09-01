using System.Security.Cryptography;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Orders;
using POS.Application.Printing;
using POS.Domain.Constants;
using POS.Domain.Entities;

namespace POS.Application.QrOrdering;

public class QrOrderingService(IAppDbContext db, OrderPrintingService printing, ICurrentUserService currentUser, QrTokenService qrTokens)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SessionLocks = new();
    public Task<List<CarPickupBayDto>> GetBaysAsync(Guid branchId, CancellationToken ct = default) => db.CarPickupBays.Where(x => x.BranchId == ScopedBranch(branchId)).OrderBy(x => x.BayLabel).Select(x => new CarPickupBayDto(x.Id, x.BranchId, x.BayLabel, x.IsActive)).ToListAsync(ct);
    public async Task<List<OrderingPointDto>> GetPointsAsync(Guid branchId, CancellationToken ct = default)
    {
        var rows = await db.OrderingPoints.Where(x => x.BranchId == ScopedBranch(branchId)).OrderBy(x => x.PointType)
            .Select(x => new { Point = x, Label = x.LinkedTable != null ? x.LinkedTable.Label : x.LinkedCarBay!.BayLabel, SessionId = x.Sessions.Where(s => s.Status == OrderingSessionStatuses.Open).Select(s => (Guid?)s.Id).FirstOrDefault() }).ToListAsync(ct);
        return rows.Select(x => new OrderingPointDto(x.Point.Id, x.Point.BranchId, x.Point.PointType, x.Point.LinkedTableId, x.Point.LinkedCarBayId, qrTokens.Generate(x.Point.Id, x.Point.QrTokenVersion), x.Point.IsActive, x.Label, x.SessionId)).ToList();
    }

    public async Task<CarPickupBayDto> SaveBayAsync(Guid? id, SaveCarPickupBayRequest request, CancellationToken ct = default)
    {
        var branchId = ScopedBranch(request.BranchId);
        if (string.IsNullOrWhiteSpace(request.BayLabel)) throw new ValidationException("Bay label is required.");
        var bay = id is null ? new CarPickupBay { Id = Guid.NewGuid(), BranchId = branchId } : await db.CarPickupBays.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, ct) ?? throw new NotFoundException("Car bay not found.");
        if (id is null) db.CarPickupBays.Add(bay);
        bay.BayLabel = request.BayLabel.Trim(); bay.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new(bay.Id, bay.BranchId, bay.BayLabel, bay.IsActive);
    }

    public async Task<OrderingPointDto> SavePointAsync(Guid? id, SaveOrderingPointRequest request, CancellationToken ct = default)
    {
        ValidatePoint(request); var branchId = ScopedBranch(request.BranchId);
        if (request.PointType == OrderingPointTypes.Table && !await db.RestaurantTables.AnyAsync(x => x.Id == request.LinkedTableId && x.BranchId == branchId && x.IsActive, ct)) throw new ValidationException("Table is invalid or inactive for this branch.");
        if (request.PointType == OrderingPointTypes.CarBay && (!await CarPickupEnabled(branchId, ct) || !await db.CarPickupBays.AnyAsync(x => x.Id == request.LinkedCarBayId && x.BranchId == branchId && x.IsActive, ct))) throw new ValidationException("Car pickup is disabled or the bay is inactive.");
        var point = id is null ? new OrderingPoint { Id = Guid.NewGuid(), BranchId = branchId, QrCodeToken = Token() } : await db.OrderingPoints.FirstOrDefaultAsync(x => x.Id == id && x.BranchId == branchId, ct) ?? throw new NotFoundException("Ordering point not found.");
        if (id is null) db.OrderingPoints.Add(point);
        point.PointType = request.PointType; point.LinkedTableId = request.LinkedTableId; point.LinkedCarBayId = request.LinkedCarBayId; point.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        var label = request.PointType == OrderingPointTypes.Table ? (await db.RestaurantTables.FirstAsync(x => x.Id == request.LinkedTableId, ct)).Label : (await db.CarPickupBays.FirstAsync(x => x.Id == request.LinkedCarBayId, ct)).BayLabel;
        return new(point.Id, point.BranchId, point.PointType, point.LinkedTableId, point.LinkedCarBayId, qrTokens.Generate(point.Id, point.QrTokenVersion), point.IsActive, label, null);
    }

    public async Task<string> RegenerateAsync(Guid id, CancellationToken ct = default)
    {
        var point = await db.OrderingPoints.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Ordering point not found."); ScopedBranch(point.BranchId);
        if (await db.OrderingSessions.AnyAsync(x => x.OrderingPointId == id && x.Status == OrderingSessionStatuses.Open, ct)) throw new ValidationException("Close the active session before regenerating its QR code.");
        point.QrTokenVersion++; point.QrCodeToken = Token(); await db.SaveChangesAsync(ct); return qrTokens.Generate(point.Id, point.QrTokenVersion);
    }

    public async Task<QrSessionDto> ResolveAsync(string token, CancellationToken ct = default)
    {
        var point = await ActivePoint(token, ct);
        return await ResolvePoint(point, ct);
    }

    public async Task<QrSessionDto> ResolveSignedAsync(Guid pointId, string token, CancellationToken ct = default)
    {
        var point = await ActivePoint(pointId, token, ct);
        return await ResolvePoint(point, ct);
    }

    private async Task<QrSessionDto> ResolvePoint(OrderingPoint point, CancellationToken ct)
    {
        var session = await db.OrderingSessions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.OrderingPointId == point.Id && x.Status == OrderingSessionStatuses.Open, ct);
        if (session is null)
        {
            session = new() { Id = Guid.NewGuid(), OrderingPointId = point.Id, AccessToken = Token(), Status = OrderingSessionStatuses.Open, OpenedAt = DateTime.UtcNow }; db.OrderingSessions.Add(session);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { db.OrderingSessions.Remove(session); session = await db.OrderingSessions.IgnoreQueryFilters().FirstAsync(x => x.OrderingPointId == point.Id && x.Status == OrderingSessionStatuses.Open, ct); }
        }
        if (string.IsNullOrWhiteSpace(session.AccessToken)) { session.AccessToken = Token(); await db.SaveChangesAsync(ct); }
        return Session(point, session);
    }

    public async Task<List<QrMenuCategoryDto>> GetMenuAsync(Guid sessionId, string accessToken, CancellationToken ct = default)
    {
        var session = await OpenSession(sessionId, accessToken, ct);
        var branchId = session.OrderingPoint.BranchId;
        var disabled = db.CategoryBranchAvailabilities.IgnoreQueryFilters().Where(x => x.BranchId == branchId && !x.IsAvailable).Select(x => x.CategoryId);
        var categories = await db.MenuCategories.AsNoTracking().Where(x => x.IsActive && !disabled.Contains(x.Id)).OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn).ToListAsync(ct);
        var ids = categories.Select(x => x.Id).ToList();
        var items = await db.MenuItems.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.CategoryId)).OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn).ToListAsync(ct);
        return categories.Select(x => new QrMenuCategoryDto(x.Id, x.NameAr, x.NameEn, items.Where(i => i.CategoryId == x.Id).Select(i => new QrMenuItemDto(i.Id, i.CategoryId, i.NameAr, i.NameEn, i.Kind, i.BasePrice, i.ImageUrl)).ToList())).Where(x => x.Items.Count > 0).ToList();
    }

    public async Task<RestaurantOrderDto> AddAsync(Guid sessionId, AddQrOrderRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count is 0 or > 30 || request.Lines.Any(x => x.Quantity is <= 0 or > 50 || x.Notes?.Length > 500)) throw new ValidationException("Order lines or quantities are invalid.");
        var gate = SessionLocks.GetOrAdd(sessionId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
        var session = await OpenSession(sessionId, request.AccessToken ?? request.QrCodeToken ?? string.Empty, ct); var point = session.OrderingPoint;
        var channelCode = point.PointType == OrderingPointTypes.Table ? "QR_TABLE" : "QR_CAR";
        var availability = await db.BranchSalesChannelAvailabilities.IgnoreQueryFilters().Include(x => x.SalesChannel).FirstOrDefaultAsync(x => x.BranchId == point.BranchId && x.SalesChannel.Code == channelCode && x.SalesChannel.IsActive && x.IsEnabled, ct) ?? throw new ValidationException("The QR sales channel is unavailable at this branch.");
        var typeCode = point.PointType == OrderingPointTypes.Table ? "DINE_IN" : "CAR_PICKUP"; var type = await db.OrderTypes.FirstAsync(x => x.Code == typeCode, ct);
        var order = await db.RestaurantOrders.IgnoreQueryFilters().Include(x => x.Items).ThenInclude(x => x.Modifiers).Include(x => x.Items).ThenInclude(x => x.ComboSelections).FirstOrDefaultAsync(x => x.OrderingSessionId == sessionId, ct);
        if (order is not null && order.Status != RestaurantOrderStatuses.Open) throw new ValidationException("This session invoice has already been sent.");
        if (order is null)
        {
            order = new() { Id = Guid.NewGuid(), BranchId = point.BranchId, OrderNumber = await db.ClaimNextOrderNumberAsync(point.BranchId, ct), OrderTypeId = type.Id, TableId = point.LinkedTableId, CarPlateNumber = point.LinkedCarBay?.BayLabel, SalesChannelId = availability.SalesChannelId, OrderingSessionId = sessionId, BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow, Status = RestaurantOrderStatuses.Open };
            db.RestaurantOrders.Add(order);
        }
        else if (order.SalesChannelId != availability.SalesChannelId) throw new ValidationException("The session invoice channel is invalid.");
        await AddLines(order, point.BranchId, request.Lines, ct);
        order.Subtotal = order.Items.Where(x => !x.IsCancelled).Sum(x => x.LineTotal); order.GrandTotal = order.Subtotal - order.DiscountAmount;
        await db.SaveChangesAsync(ct);
        return RestaurantOrderService.ToDto(order, type.Code);
        }
        finally { gate.Release(); }
    }

    public async Task ConfirmAsync(Guid orderId, ConfirmQrOrderRequest request, CancellationToken ct = default)
    {
        var gate = SessionLocks.GetOrAdd(request.SessionId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
        var session = await OpenSession(request.SessionId, request.AccessToken ?? request.QrCodeToken ?? string.Empty, ct);
        var order = await db.RestaurantOrders.IgnoreQueryFilters().Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == orderId && x.OrderingSessionId == session.Id && x.BranchId == session.OrderingPoint.BranchId, ct) ?? throw new NotFoundException("QR order not found.");
        if (order.Status == RestaurantOrderStatuses.Sent) return;
        var config = await db.BranchSalesChannelAvailabilities.IgnoreQueryFilters().Include(x => x.SalesChannel).FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.SalesChannelId == order.SalesChannelId && x.IsEnabled && x.SalesChannel.IsActive, ct) ?? throw new ValidationException("The QR sales channel is disabled.");
        if (config.RequiresPrepayment && order.Payments.Sum(x => x.Amount) < order.GrandTotal) throw new ValidationException("Full prepayment is required before confirmation.");
        await printing.ConfirmQrAndPrintAsync(orderId, order.BranchId, ct);
        }
        finally { gate.Release(); }
    }

    public async Task CloseAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await db.OrderingSessions.Include(x => x.OrderingPoint).FirstOrDefaultAsync(x => x.Id == sessionId && x.Status == OrderingSessionStatuses.Open, ct) ?? throw new NotFoundException("Open session not found."); ScopedBranch(session.OrderingPoint.BranchId);
        var order = await db.RestaurantOrders.IgnoreQueryFilters().Include(x => x.Payments).FirstOrDefaultAsync(x => x.OrderingSessionId == sessionId, ct);
        if (order is not null && order.Payments.Sum(x => x.Amount) < order.GrandTotal) throw new ValidationException("The invoice must be fully settled before closing the session.");
        session.Status = OrderingSessionStatuses.Closed; session.ClosedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        SessionLocks.TryRemove(sessionId, out _);
    }

    private async Task AddLines(RestaurantOrder order, Guid branchId, List<CreateOrderLineRequest> inputs, CancellationToken ct)
    {
        var ids = inputs.Select(x => x.MenuItemId).Distinct().ToList();
        var disabledCategories = db.CategoryBranchAvailabilities.IgnoreQueryFilters().Where(x => x.BranchId == branchId && !x.IsAvailable).Select(x => x.CategoryId);
        var items = await db.MenuItems.Where(x => ids.Contains(x.Id) && x.IsActive && !disabledCategories.Contains(x.CategoryId)).Include(x => x.ModifierGroups).ThenInclude(x => x.ModifierGroup).ThenInclude(x => x.Options).Include(x => x.ComboComponents).ThenInclude(x => x.Options).ToDictionaryAsync(x => x.Id, ct);
        if (items.Count != ids.Count) throw new ValidationException("Menu item unavailable.");
        foreach (var input in inputs)
        {
            var item = items[input.MenuItemId]; decimal delta = 0;
            var line = new RestaurantOrderItem { Id = Guid.NewGuid(), MenuItemId = item.Id, MenuItemNameSnapshot = item.NameEn, Quantity = input.Quantity, Notes = input.Notes?.Trim() };
            if (item.Kind == MenuItemKinds.SingleProduct)
            {
                var selected = input.ModifierOptionIds.Distinct().ToList();
                foreach (var link in item.ModifierGroups)
                {
                    var group = link.ModifierGroup; var chosen = group.Options.Where(x => selected.Contains(x.Id) && x.IsActive).ToList(); var min = group.IsRequired ? Math.Max(1, group.MinSelect) : group.MinSelect;
                    if (chosen.Count < min || chosen.Count > group.MaxSelect) throw new ValidationException($"Invalid selection for {group.NameEn}.");
                    foreach (var option in chosen) { delta += option.PriceDelta; line.Modifiers.Add(new() { Id = Guid.NewGuid(), ModifierOptionId = option.Id, PriceDeltaSnapshot = option.PriceDelta }); }
                }
                if (selected.Any(x => !line.Modifiers.Any(m => m.ModifierOptionId == x))) throw new ValidationException("Modifier unavailable.");
            }
            else foreach (var component in item.ComboComponents)
            {
                var chosen = input.ComboSelections.Where(x => x.ComboComponentId == component.Id).ToList(); var min = component.IsRequired ? Math.Max(1, component.MinSelect) : component.MinSelect;
                if (chosen.Count < min || chosen.Count > component.MaxSelect) throw new ValidationException($"Invalid selection for {component.SlotLabel}.");
                foreach (var selection in chosen)
                {
                    var option = component.Options.SingleOrDefault(x => x.MenuItemId == selection.SelectedMenuItemId) ?? throw new ValidationException("Combo option unavailable.");
                    delta += option.PriceDelta; line.ComboSelections.Add(new() { Id = Guid.NewGuid(), ComboComponentId = component.Id, SelectedMenuItemId = option.MenuItemId, PriceDeltaSnapshot = option.PriceDelta });
                }
            }
            line.UnitPriceSnapshot = item.BasePrice + delta; line.LineTotal = line.UnitPriceSnapshot * line.Quantity; order.Items.Add(line); db.RestaurantOrderItems.Add(line);
        }
    }

    public async Task TransferOrderAsync(Guid orderId, Guid newOrderingPointId, Guid userId, string? notes, CancellationToken ct = default)
    {
        var order = await db.RestaurantOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");
        if (order.Status is RestaurantOrderStatuses.Paid or RestaurantOrderStatuses.Closed) throw new ValidationException("Paid or closed orders cannot be transferred.");
        var point = await db.OrderingPoints.FirstOrDefaultAsync(x => x.Id == newOrderingPointId && x.IsActive && x.BranchId == order.BranchId, ct) ?? throw new ValidationException("The target ordering point is inactive or belongs to another branch.");
        var session = await db.OrderingSessions.FirstOrDefaultAsync(x => x.OrderingPointId == point.Id && x.Status == OrderingSessionStatuses.Open, ct);
        if (session is null) { session = new() { Id = Guid.NewGuid(), OrderingPointId = point.Id, AccessToken = Token(), Status = OrderingSessionStatuses.Open, OpenedAt = DateTime.UtcNow }; db.OrderingSessions.Add(session); }
        else if (await db.RestaurantOrders.AnyAsync(x => x.OrderingSessionId == session.Id && x.Id != order.Id, ct)) throw new ValidationException("The target ordering point already has another invoice.");
        var previousSessionId = order.OrderingSessionId; order.OrderingSessionId = session.Id;
        db.OrderEditLogs.Add(new() { Id = Guid.NewGuid(), OrderId = order.Id, UserId = userId, EditType = "Transferred", Notes = $"{notes?.Trim()} From session {previousSessionId?.ToString() ?? "none"} to point {newOrderingPointId}".Trim(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    private async Task<OrderingSession> OpenSession(Guid sessionId, string token, CancellationToken ct)
    {
        var session = await db.OrderingSessions.IgnoreQueryFilters().Include(x => x.OrderingPoint).ThenInclude(x => x.Branch).Include(x => x.OrderingPoint).ThenInclude(x => x.LinkedTable).Include(x => x.OrderingPoint).ThenInclude(x => x.LinkedCarBay).FirstOrDefaultAsync(x => x.Id == sessionId && x.Status == OrderingSessionStatuses.Open && x.AccessToken == token, ct) ?? throw new NotFoundException("Open ordering session not found.");
        ValidateActivePoint(session.OrderingPoint);
        if (session.OrderingPoint.PointType == OrderingPointTypes.CarBay && !await CarPickupEnabled(session.OrderingPoint.BranchId, ct)) throw new ValidationException("Car pickup is disabled at this branch.");
        return session;
    }

    private async Task<OrderingPoint> ActivePoint(string token, CancellationToken ct)
    {
        var point = await db.OrderingPoints.IgnoreQueryFilters().Include(x => x.Branch).Include(x => x.LinkedTable).Include(x => x.LinkedCarBay).FirstOrDefaultAsync(x => x.QrCodeToken == token, ct) ?? throw new NotFoundException("QR code is invalid or disabled.");
        ValidateActivePoint(point);
        if (point.PointType == OrderingPointTypes.CarBay && !await CarPickupEnabled(point.BranchId, ct)) throw new NotFoundException("QR code is invalid or disabled.");
        return point;
    }

    private async Task<OrderingPoint> ActivePoint(Guid pointId, string token, CancellationToken ct)
    {
        var point = await db.OrderingPoints.IgnoreQueryFilters().Include(x => x.Branch).Include(x => x.LinkedTable).Include(x => x.LinkedCarBay).FirstOrDefaultAsync(x => x.Id == pointId, ct) ?? throw new NotFoundException("QR code is invalid or disabled.");
        if (!qrTokens.Verify(point.Id, point.QrTokenVersion, token)) throw new NotFoundException("QR code is invalid or disabled.");
        ValidateActivePoint(point);
        if (point.PointType == OrderingPointTypes.CarBay && !await CarPickupEnabled(point.BranchId, ct)) throw new NotFoundException("QR code is invalid or disabled.");
        return point;
    }

    private static void ValidateActivePoint(OrderingPoint point)
    {
        if (!point.IsActive || !point.Branch.IsActive || point.PointType == OrderingPointTypes.Table && point.LinkedTable?.IsActive != true || point.PointType == OrderingPointTypes.CarBay && point.LinkedCarBay?.IsActive != true) throw new NotFoundException("QR code is invalid or disabled.");
    }

    private Task<bool> CarPickupEnabled(Guid branchId, CancellationToken ct) => db.BranchFeatureFlags.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branchId && x.FeatureKey == BranchFeatureKeys.CarPickup && x.IsEnabled, ct);
    private Guid ScopedBranch(Guid requested) { if (!currentUser.BypassBranchFilter && currentUser.BranchId != requested) throw new ValidationException("You do not have access to this branch."); return currentUser.BypassBranchFilter ? requested : currentUser.BranchId ?? throw new ValidationException("A branch assignment is required."); }
    private static void ValidatePoint(SaveOrderingPointRequest request) { if (!OrderingPointTypes.All.Contains(request.PointType) || (request.PointType == OrderingPointTypes.Table) != (request.LinkedTableId is not null) || (request.PointType == OrderingPointTypes.CarBay) != (request.LinkedCarBayId is not null)) throw new ValidationException("Ordering point link does not match its type."); }
    private static string Token() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    private static QrSessionDto Session(OrderingPoint point, OrderingSession session) => new(session.Id, point.Id, point.BranchId, point.PointType, point.LinkedTable?.Label ?? point.LinkedCarBay!.BayLabel, session.OpenedAt, session.AccessToken);
}
