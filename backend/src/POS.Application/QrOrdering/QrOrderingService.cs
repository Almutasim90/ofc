using System.Security.Cryptography;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using POS.Application.Abstractions;
using POS.Application.Common;
using POS.Application.Orders;
using POS.Application.Printing;
using POS.Domain.Constants;
using POS.Domain.Entities;
using POS.Application.Invoices;

namespace POS.Application.QrOrdering;

public class QrOrderingService(IAppDbContext db, OrderPrintingService printing, ICurrentUserService currentUser, QrTokenService qrTokens)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> SessionLocks = new();
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> ApprovalLocks = new();
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
        await EnsureQrAvailable(point.BranchId, ct);
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
        var items = await db.MenuItems.AsNoTracking().Where(x => x.IsActive && ids.Contains(x.CategoryId))
            .Include(x => x.ModifierGroups).ThenInclude(x => x.ModifierGroup).ThenInclude(x => x.Options)
            .Include(x => x.ComboComponents).ThenInclude(x => x.Options).ThenInclude(x => x.MenuItem)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.NameEn).ToListAsync(ct);
        return categories.Select(category => new QrMenuCategoryDto(category.Id, category.NameAr, category.NameEn,
            items.Where(item => item.CategoryId == category.Id).Select(item => new QrMenuItemDto(item.Id, item.CategoryId, item.NameAr, item.NameEn, item.Kind, item.BasePrice, item.ImageUrl,
                item.ModifierGroups.Select(link => link.ModifierGroup).Select(group => new QrModifierGroupDto(group.Id, group.NameAr, group.NameEn, group.MinSelect, group.MaxSelect, group.IsRequired,
                    group.Options.Where(option => option.IsActive).Select(option => new QrModifierOptionDto(option.Id, option.NameAr, option.NameEn, option.PriceDelta)).ToList())).ToList(),
                item.ComboComponents.OrderBy(component => component.SortOrder).Select(component => new QrComboComponentDto(component.Id, component.SlotLabel, component.MinSelect, component.MaxSelect, component.IsRequired,
                    component.Options.Where(option => option.MenuItem.IsActive).Select(option => new QrComboOptionDto(option.MenuItemId, option.MenuItem.NameAr, option.MenuItem.NameEn, option.PriceDelta, option.IsDefault)).ToList())).ToList())).ToList()))
            .Where(category => category.Items.Count > 0).ToList();
    }

    public async Task ValidateSessionAsync(Guid sessionId, string accessToken, CancellationToken ct = default) => _ = await AuthenticatedSession(sessionId, accessToken, ct);

    public Task<List<BranchQrScheduleDto>> GetSchedulesAsync(Guid branchId, CancellationToken ct = default)
    {
        branchId = ScopedBranch(branchId);
        return db.BranchQrOrderingSchedules.Where(x => x.BranchId == branchId).OrderBy(x => x.DayOfWeek).Select(x => new BranchQrScheduleDto(x.Id, x.BranchId, x.DayOfWeek, x.OpensAt, x.ClosesAt, x.IsEnabled)).ToListAsync(ct);
    }

    public async Task<BranchQrScheduleDto> SaveScheduleAsync(Guid branchId, SaveBranchQrScheduleRequest request, CancellationToken ct = default)
    {
        branchId = ScopedBranch(branchId);
        if (request.DayOfWeek is < 0 or > 6 || request.OpensAt == request.ClosesAt) throw new ValidationException("QR schedule day and times are invalid.");
        var row = await db.BranchQrOrderingSchedules.FirstOrDefaultAsync(x => x.BranchId == branchId && x.DayOfWeek == request.DayOfWeek, ct);
        if (row is null) { row = new() { Id = Guid.NewGuid(), BranchId = branchId, DayOfWeek = request.DayOfWeek }; db.BranchQrOrderingSchedules.Add(row); }
        row.OpensAt = request.OpensAt; row.ClosesAt = request.ClosesAt; row.IsEnabled = request.IsEnabled; await db.SaveChangesAsync(ct);
        return new(row.Id, row.BranchId, row.DayOfWeek, row.OpensAt, row.ClosesAt, row.IsEnabled);
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
        var order = await db.RestaurantOrders.IgnoreQueryFilters().Include(x => x.Items).ThenInclude(x => x.Modifiers).ThenInclude(x => x.ModifierOption).Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.ComboComponent).Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.SelectedMenuItem).FirstOrDefaultAsync(x => x.OrderingSessionId == sessionId, ct);
        if (order is not null && order.Status != RestaurantOrderStatuses.Open) throw new ValidationException("This session invoice has already been sent.");
        if (order is null)
        {
            order = new() { Id = Guid.NewGuid(), BranchId = point.BranchId, Branch = point.Branch, OrderNumber = await db.ClaimNextOrderNumberAsync(point.BranchId, ct), OrderTypeId = type.Id, TableId = point.LinkedTableId, CarPlateNumber = point.LinkedCarBay?.BayLabel, SalesChannelId = availability.SalesChannelId, OrderingSessionId = sessionId, BusinessDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow, Status = RestaurantOrderStatuses.Open };
            var invoiceSettings = await db.InvoiceSettings.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.BranchId == point.BranchId, ct);
            InvoiceService.ApplySettings(order, invoiceSettings is null ? new(point.BranchId, point.Branch.NameAr, point.Branch.NameEn, null, null, null, null, null, "OMR", false, 0, null) : new(invoiceSettings.BranchId, invoiceSettings.LegalNameAr, invoiceSettings.LegalNameEn, invoiceSettings.TaxRegistrationNumber, invoiceSettings.CommercialRegistrationNumber, invoiceSettings.AddressAr, invoiceSettings.AddressEn, invoiceSettings.Phone, invoiceSettings.Currency, invoiceSettings.PricesIncludeTax, invoiceSettings.DefaultTaxRate, invoiceSettings.Footer));
            db.RestaurantOrders.Add(order);
        }
        else if (order.SalesChannelId != availability.SalesChannelId) throw new ValidationException("The session invoice channel is invalid.");
        await AddLines(order, point.BranchId, request.Lines, ct);
        InvoiceService.CalculateOrder(order);
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
        if (order.Status == RestaurantOrderStatuses.PendingApproval) return;
        if (order.Status != RestaurantOrderStatuses.Open) throw new ValidationException("Only an open QR order can be submitted.");
        var config = await db.BranchSalesChannelAvailabilities.IgnoreQueryFilters().Include(x => x.SalesChannel).FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.SalesChannelId == order.SalesChannelId && x.IsEnabled && x.SalesChannel.IsActive, ct) ?? throw new ValidationException("The QR sales channel is disabled.");
        if (config.RequiresPrepayment && order.Payments.Sum(x => x.Amount) < order.GrandTotal) throw new ValidationException("Full prepayment is required before confirmation.");
        order.Status = RestaurantOrderStatuses.PendingApproval; order.SubmittedAt = DateTime.UtcNow; await db.SaveChangesAsync(ct);
        }
        finally { gate.Release(); }
    }

    public async Task<RestaurantOrderDto> GetSessionOrderAsync(Guid sessionId, string accessToken, CancellationToken ct = default)
    {
        var session = await AuthenticatedSession(sessionId, accessToken, ct);
        var order = await db.RestaurantOrders.IgnoreQueryFilters().Include(x => x.OrderType).Include(x => x.Items).ThenInclude(x => x.Modifiers).ThenInclude(x => x.ModifierOption).Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.ComboComponent).Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.SelectedMenuItem).FirstOrDefaultAsync(x => x.OrderingSessionId == session.Id, ct) ?? throw new NotFoundException("QR order not found.");
        return RestaurantOrderService.ToDto(order, order.OrderType.Code, session.OrderingPoint.LinkedTable?.Label);
    }

    public async Task ApproveAsync(Guid orderId, CancellationToken ct = default)
    {
        var gate = ApprovalLocks.GetOrAdd(orderId, _ => new(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var order = await db.RestaurantOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found."); ScopedBranch(order.BranchId);
            if (order.Status == RestaurantOrderStatuses.Paid && order.ApprovedAt is not null) return;
            if (order.Status == RestaurantOrderStatuses.Sent && order.ApprovedAt is not null) { await printing.ConfirmQrAndPrintAsync(order.Id, order.BranchId, ct); return; }
            if (order.Status != RestaurantOrderStatuses.PendingApproval) throw new ValidationException("Only pending QR orders can be approved.");
            var config = await db.BranchSalesChannelAvailabilities.Include(x => x.SalesChannel).FirstOrDefaultAsync(x => x.BranchId == order.BranchId && x.SalesChannelId == order.SalesChannelId && x.IsEnabled && x.SalesChannel.IsActive, ct) ?? throw new ValidationException("The QR sales channel is disabled.");
            if (config.RequiresPrepayment && await db.OrderPayments.Where(x => x.OrderId == order.Id).SumAsync(x => x.Amount, ct) < order.GrandTotal) throw new ValidationException("Full prepayment is required before approval.");
            order.ApprovedAt = DateTime.UtcNow; order.ApprovedByUserId = currentUser.UserId ?? throw new ValidationException("Authenticated user is required.");
            await printing.ConfirmQrAndPrintAsync(order.Id, order.BranchId, ct);
            if (await db.OrderPayments.Where(x => x.OrderId == order.Id).SumAsync(x => x.Amount, ct) >= order.GrandTotal) { order.Status = RestaurantOrderStatuses.Paid; InvoiceService.CaptureCompletedSnapshot(order); await db.SaveChangesAsync(ct); }
        }
        finally { gate.Release(); }
    }

    public async Task RejectAsync(Guid orderId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new ValidationException("A rejection reason is required.");
        var order = await db.RestaurantOrders.Include(x => x.Items).Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found."); ScopedBranch(order.BranchId);
        if (order.Status != RestaurantOrderStatuses.PendingApproval) throw new ValidationException("Only pending QR orders can be rejected.");
        if (order.Payments.Count > 0) throw new ValidationException("A paid QR order must be refunded before it can be rejected.");
        order.Status = RestaurantOrderStatuses.Cancelled; order.RejectedAt = DateTime.UtcNow; order.RejectionReason = reason.Trim(); foreach (var line in order.Items) line.IsCancelled = true;
        if (order.OrderingSessionId.HasValue) { var session = await db.OrderingSessions.IgnoreQueryFilters().SingleAsync(x => x.Id == order.OrderingSessionId.Value, ct); session.Status = OrderingSessionStatuses.Closed; session.ClosedAt = DateTime.UtcNow; }
        db.OrderEditLogs.Add(new() { Id = Guid.NewGuid(), OrderId = order.Id, UserId = currentUser.UserId ?? throw new ValidationException("Authenticated user is required."), EditType = "QrRejected", Notes = order.RejectionReason, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    public async Task<RestaurantOrderDto> EditPendingAsync(Guid orderId, EditPendingQrOrderRequest request, CancellationToken ct = default)
    {
        if (request.Lines.Count is 0 or > 30 || request.Lines.Any(x => x.Quantity is <= 0 or > 50 || x.Notes?.Length > 500)) throw new ValidationException("Order lines or quantities are invalid.");
        var order = await db.RestaurantOrders.Include(x => x.OrderType).Include(x => x.Table).Include(x => x.Payments).Include(x => x.Items).ThenInclude(x => x.Modifiers).ThenInclude(x => x.ModifierOption).Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.ComboComponent).Include(x => x.Items).ThenInclude(x => x.ComboSelections).ThenInclude(x => x.SelectedMenuItem).FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found."); ScopedBranch(order.BranchId);
        if (order.Status != RestaurantOrderStatuses.PendingApproval) throw new ValidationException("Only pending QR orders can be edited.");
        db.RestaurantOrderItems.RemoveRange(order.Items); order.Items.Clear(); await AddLines(order, order.BranchId, request.Lines, ct); order.DiscountAmount = 0; InvoiceService.CalculateOrder(order);
        if (order.Payments.Sum(x => x.Amount) > order.GrandTotal) throw new ValidationException("The edited total cannot be less than payments already recorded.");
        order.PaymentRevision++;
        db.OrderEditLogs.Add(new() { Id = Guid.NewGuid(), OrderId = order.Id, UserId = currentUser.UserId ?? throw new ValidationException("Authenticated user is required."), EditType = "QrEdited", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct); return RestaurantOrderService.ToDto(order, order.OrderType.Code, order.Table?.Label);
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
        var items = await db.MenuItems.Where(x => ids.Contains(x.Id) && x.IsActive && !disabledCategories.Contains(x.CategoryId)).Include(x => x.ModifierGroups).ThenInclude(x => x.ModifierGroup).ThenInclude(x => x.Options).Include(x => x.ComboComponents).ThenInclude(x => x.Options).ThenInclude(x => x.MenuItem).ToDictionaryAsync(x => x.Id, ct);
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
                    foreach (var option in chosen) { delta += option.PriceDelta; line.Modifiers.Add(new() { Id = Guid.NewGuid(), ModifierOptionId = option.Id, ModifierOption = option, PriceDeltaSnapshot = option.PriceDelta }); }
                }
                if (selected.Any(x => !line.Modifiers.Any(m => m.ModifierOptionId == x))) throw new ValidationException("Modifier unavailable.");
            }
            else
            {
                if (input.ComboSelections.GroupBy(x => new { x.ComboComponentId, x.SelectedMenuItemId }).Any(x => x.Count() > 1)) throw new ValidationException("Duplicate combo selection.");
                if (input.ComboSelections.Any(x => item.ComboComponents.All(component => component.Id != x.ComboComponentId))) throw new ValidationException("Combo component unavailable.");
                foreach (var component in item.ComboComponents)
                {
                var chosen = input.ComboSelections.Where(x => x.ComboComponentId == component.Id).ToList(); var min = component.IsRequired ? Math.Max(1, component.MinSelect) : component.MinSelect;
                if (chosen.Count < min || chosen.Count > component.MaxSelect) throw new ValidationException($"Invalid selection for {component.SlotLabel}.");
                foreach (var selection in chosen)
                {
                    var option = component.Options.SingleOrDefault(x => x.MenuItemId == selection.SelectedMenuItemId && x.MenuItem.IsActive) ?? throw new ValidationException("Combo option unavailable.");
                    delta += option.PriceDelta; line.ComboSelections.Add(new() { Id = Guid.NewGuid(), ComboComponentId = component.Id, ComboComponent = component, SelectedMenuItemId = option.MenuItemId, SelectedMenuItem = option.MenuItem, PriceDeltaSnapshot = option.PriceDelta });
                }
                }
            }
            line.UnitPriceSnapshot = item.BasePrice + delta; line.LineTotal = line.UnitPriceSnapshot * line.Quantity; order.Items.Add(line); db.RestaurantOrderItems.Add(line);
        }
    }

    public async Task TransferOrderAsync(Guid orderId, Guid newOrderingPointId, Guid userId, string? notes, CancellationToken ct = default)
    {
        var order = await db.RestaurantOrders.FirstOrDefaultAsync(x => x.Id == orderId, ct) ?? throw new NotFoundException("Order not found.");
        if (order.Status is RestaurantOrderStatuses.Paid or RestaurantOrderStatuses.Closed) throw new ValidationException("Paid or closed orders cannot be transferred.");
        var point = await db.OrderingPoints.Include(x => x.LinkedTable).Include(x => x.LinkedCarBay).FirstOrDefaultAsync(x => x.Id == newOrderingPointId && x.IsActive && x.BranchId == order.BranchId, ct) ?? throw new ValidationException("The target ordering point is inactive or belongs to another branch.");
        var session = await db.OrderingSessions.FirstOrDefaultAsync(x => x.OrderingPointId == point.Id && x.Status == OrderingSessionStatuses.Open, ct);
        if (session is null) { session = new() { Id = Guid.NewGuid(), OrderingPointId = point.Id, AccessToken = Token(), Status = OrderingSessionStatuses.Open, OpenedAt = DateTime.UtcNow }; db.OrderingSessions.Add(session); }
        else if (await db.RestaurantOrders.AnyAsync(x => x.OrderingSessionId == session.Id && x.Id != order.Id, ct)) throw new ValidationException("The target ordering point already has another invoice.");
        var orderTypeCode = point.PointType == OrderingPointTypes.Table ? "DINE_IN" : "CAR_PICKUP";
        var orderTypeId = await db.OrderTypes.Where(x => x.Code == orderTypeCode).Select(x => x.Id).FirstAsync(ct);
        var previousSessionId = order.OrderingSessionId;
        order.OrderingSessionId = session.Id;
        order.OrderTypeId = orderTypeId;
        order.TableId = point.LinkedTableId;
        order.CarPlateNumber = point.PointType == OrderingPointTypes.CarBay ? point.LinkedCarBay!.BayLabel : null;
        db.OrderEditLogs.Add(new() { Id = Guid.NewGuid(), OrderId = order.Id, UserId = userId, EditType = "Transferred", Notes = $"{notes?.Trim()} From session {previousSessionId?.ToString() ?? "none"} to point {newOrderingPointId}".Trim(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
    }

    private async Task<OrderingSession> OpenSession(Guid sessionId, string token, CancellationToken ct)
    {
        var session = await AuthenticatedSession(sessionId, token, ct);
        ValidateActivePoint(session.OrderingPoint);
        await EnsureQrAvailable(session.OrderingPoint.BranchId, ct);
        if (session.OrderingPoint.PointType == OrderingPointTypes.CarBay && !await CarPickupEnabled(session.OrderingPoint.BranchId, ct)) throw new ValidationException("Car pickup is disabled at this branch.");
        return session;
    }

    private async Task<OrderingSession> AuthenticatedSession(Guid sessionId, string token, CancellationToken ct) =>
        await db.OrderingSessions.IgnoreQueryFilters().Include(x => x.OrderingPoint).ThenInclude(x => x.Branch).Include(x => x.OrderingPoint).ThenInclude(x => x.LinkedTable).Include(x => x.OrderingPoint).ThenInclude(x => x.LinkedCarBay).FirstOrDefaultAsync(x => x.Id == sessionId && x.Status == OrderingSessionStatuses.Open && x.AccessToken == token, ct) ?? throw new NotFoundException("Open ordering session not found.");

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
    private async Task EnsureQrAvailable(Guid branchId, CancellationToken ct)
    {
        if (await db.BranchFeatureFlags.IgnoreQueryFilters().AnyAsync(x => x.BranchId == branchId && x.FeatureKey == BranchFeatureKeys.QrOrdering && !x.IsEnabled, ct)) throw new ValidationException("QR ordering is disabled at this branch.");
        var schedules = await db.BranchQrOrderingSchedules.IgnoreQueryFilters().Where(x => x.BranchId == branchId).ToListAsync(ct);
        if (schedules.Count == 0) return;
        var local = DateTime.UtcNow.AddHours(4); var day = (int)local.DayOfWeek; var previousDay = (day + 6) % 7; var time = TimeOnly.FromDateTime(local);
        var available = schedules.Any(x => x.IsEnabled && x.DayOfWeek == day && (x.OpensAt < x.ClosesAt ? time >= x.OpensAt && time < x.ClosesAt : time >= x.OpensAt)) || schedules.Any(x => x.IsEnabled && x.DayOfWeek == previousDay && x.OpensAt > x.ClosesAt && time < x.ClosesAt);
        if (!available) throw new ValidationException("QR ordering is currently closed at this branch.");
    }
    private Guid ScopedBranch(Guid requested) { if (!currentUser.BypassBranchFilter && currentUser.BranchId != requested) throw new ValidationException("You do not have access to this branch."); return currentUser.BypassBranchFilter ? requested : currentUser.BranchId ?? throw new ValidationException("A branch assignment is required."); }
    private static void ValidatePoint(SaveOrderingPointRequest request) { if (!OrderingPointTypes.All.Contains(request.PointType) || (request.PointType == OrderingPointTypes.Table) != (request.LinkedTableId is not null) || (request.PointType == OrderingPointTypes.CarBay) != (request.LinkedCarBayId is not null)) throw new ValidationException("Ordering point link does not match its type."); }
    private static string Token() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    private static QrSessionDto Session(OrderingPoint point, OrderingSession session) => new(session.Id, point.Id, point.BranchId, point.PointType, point.LinkedTable?.Label ?? point.LinkedCarBay!.BayLabel, session.OpenedAt, session.AccessToken);
}
