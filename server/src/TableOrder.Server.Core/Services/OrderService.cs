namespace TableOrder.Server.Core.Services;

using System.Security.Cryptography;

using TableOrder.Contract.Events;
using TableOrder.Contract.Menu;
using TableOrder.Contract.Orders;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Infrastructure.Json;

public sealed class OrderService
{
    private readonly ServiceContextProvider contextProvider;

    private readonly IDialect dialect;

    private readonly StoreAccessor storeAccessor;

    private readonly VisitAccessor visitAccessor;

    private readonly OrderAccessor orderAccessor;

    private readonly KitchenAccessor kitchenAccessor;

    private readonly StockAccessor stockAccessor;

    private readonly MenuService menuService;

    private readonly EventService eventService;

    public OrderService(
        ServiceContextProvider contextProvider,
        IDialect dialect,
        StoreAccessor storeAccessor,
        VisitAccessor visitAccessor,
        OrderAccessor orderAccessor,
        KitchenAccessor kitchenAccessor,
        StockAccessor stockAccessor,
        MenuService menuService,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.dialect = dialect;
        this.storeAccessor = storeAccessor;
        this.visitAccessor = visitAccessor;
        this.orderAccessor = orderAccessor;
        this.kitchenAccessor = kitchenAccessor;
        this.stockAccessor = stockAccessor;
        this.menuService = menuService;
        this.eventService = eventService;
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    // 来店の注文 (注文履歴と明細の状態)
    public async ValueTask<ServiceResult<OrderListResponse>> GetListAsync(Guid visitId, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var visit = await visitAccessor.QueryAsync(tenantId, context.RequireStoreId(), visitId, cancellationToken);
        if (visit is null)
        {
            return new(ServiceError.NotFound);
        }

        if (!VisitService.InScope(context, visit))
        {
            return new(ServiceError.DeviceScope);
        }

        return new(new OrderListResponse { Items = await OrderResponses.LoadAsync(orderAccessor, tenantId, visitId, cancellationToken) });
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // 注文の送信。同じ Id の送り直しは、同じ内容なら受け付けた注文を返す (201 ではなく 200。時間帯の終わりを過ぎていても)
    // 確かめる順: 来店と店舗 → メニューとオプションと数量 → 出せる条件 → 品切れ → ルール (先に見つけた種類の誤りを、明細の Id ごとに返す)
    public async ValueTask<ServiceResult<OrderListResponseItem>> CreateAsync(Guid visitId, OrderCreateRequest request, CancellationToken cancellationToken)
    {
        if (ValidateRequest(request) is { } invalid)
        {
            return new(invalid);
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var now = context.Now;
        var hash = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request, JsonDefaults.Options));
        var source = context.DeviceKind == DeviceKind.Table ? OrderSource.Table : OrderSource.Hall;

        return await eventService.WriteAsync<ServiceResult<OrderListResponseItem>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var visit = await visitAccessor.QueryAsync(tx, tenantId, storeId, visitId, cancellationToken);
            if (visit is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!VisitService.InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            var existing = await orderAccessor.QueryAsync(tx, tenantId, storeId, request.Id, cancellationToken);
            if (existing is not null)
            {
                if ((existing.VisitId != visitId) || (await orderAccessor.CountByRequestHashAsync(tx, tenantId, request.Id, hash, cancellationToken) == 0))
                {
                    return new(new ServiceError(ErrorCodes.DuplicateIdMismatch));
                }

                var accepted = await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, visitId, cancellationToken);
                return new(accepted.First(x => x.Id == request.Id));
            }

            if (visit.Status != VisitStatus.Open)
            {
                return new(new ServiceError(visit.Status == VisitStatus.Paying ? ErrorCodes.CheckoutInProgress : ErrorCodes.VisitNotOpen));
            }

            var store = await storeAccessor.QueryAsync(tx, tenantId, storeId, cancellationToken);
            if (store is null)
            {
                return new(ServiceError.NotFound);
            }

            if (store.OrderingPaused)
            {
                return new(new ServiceError(ErrorCodes.OrderingPaused));
            }

            if (IsAfterLastOrder(store, now))
            {
                return new(new ServiceError(ErrorCodes.LastOrderPassed));
            }

            var catalog = await menuService.GetCatalogAsync(store, cancellationToken);
            if (catalog is null)
            {
                return new(new ServiceError(ErrorCodes.MenuChanged));
            }

            var plans = new List<LinePlan>();
            var error = Plan(request, store, catalog, plans) ??
                        CheckAvailability(plans, catalog, visit, StoreHours.LocalTime(now, store.TimeZone), TimeSpan.FromMinutes(SettingsService.ReadFeatures(store.Features).DaypartGraceMinutes)) ??
                        CheckStock(plans, (await stockAccessor.QueryListAsync(tx, tenantId, storeId, cancellationToken)).ToDictionary(static x => x.TargetId)) ??
                        CheckRules(
                            plans,
                            catalog,
                            visit,
                            await orderAccessor.QueryLineListAsync(tx, tenantId, visitId, cancellationToken),
                            await visitAccessor.QueryConfirmationListAsync(tx, tenantId, visitId, cancellationToken));
            if (error is not null)
            {
                return new(error);
            }

            // 作る品は持ち場ごとのチケットにする (チケットは明細より先に書く)
            var tickets = plans
                .Where(static x => x.Status == OrderLineStatus.Ordered)
                .Select(static x => x.Item.StationId!.Value)
                .Distinct()
                .ToDictionary(static x => x, _ => Guid.CreateVersion7(now));
            try
            {
                var orderNo = (int)await orderAccessor.QueryNextOrderNoAsync(tx, tenantId, visitId, cancellationToken);
                await orderAccessor.InsertAsync(tx, tenantId, request.Id, storeId, visitId, orderNo, source, context.DeviceId, catalog.Menu.MenuVersion, hash, now, cancellationToken);
                foreach (var (stationId, ticketId) in tickets)
                {
                    await kitchenAccessor.InsertTicketAsync(tx, tenantId, ticketId, storeId, stationId, request.Id, visitId, now, cancellationToken);
                }

                for (var i = 0; i < plans.Count; i++)
                {
                    await InsertLineAsync(tx, tenantId, storeId, request.Id, visitId, i + 1, plans[i], tickets, now, cancellationToken);
                }
            }
            catch (DbException ex) when (dialect.IsDuplicate(ex))
            {
                // 明細の Id がほかの注文と重なった
                return new(new ServiceError(ErrorCodes.DuplicateIdMismatch));
            }

            // 残りの数のある品だけ減らす (なくなったら売り切れ)
            var reduced = new HashSet<Guid>();
            foreach (var (targetId, quantity) in UsageOf(plans))
            {
                if (await stockAccessor.AddRemainingAsync(tx, tenantId, storeId, targetId, -quantity, now, cancellationToken) > 0)
                {
                    reduced.Add(targetId);
                }
            }

            var orders = await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, visitId, cancellationToken);
            var order = orders.First(x => x.Id == request.Id);
            await transaction.AppendEventAsync(EventTypes.OrderCreated, order, [visit.TableId], null, now, cancellationToken);
            foreach (var (stationId, ticketId) in tickets)
            {
                var ticket = await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.TicketCreated, ticket, null, stationId, now, cancellationToken);
            }

            if (reduced.Count > 0)
            {
                var stocks = (await stockAccessor.QueryListAsync(tx, tenantId, storeId, cancellationToken)).Where(x => reduced.Contains(x.TargetId));
                await transaction.AppendEventAsync(EventTypes.StockUpdated, new StockUpdatedEventData { Items = stocks.Select(StockService.ToItem).ToList() }, null, null, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new(order, created: true);
        }, cancellationToken);
    }

    private async ValueTask InsertLineAsync(DbTransaction tx, Guid tenantId, Guid storeId, Guid orderId, Guid visitId, int lineNo, LinePlan plan, Dictionary<Guid, Guid> tickets, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ticketId = plan.Status == OrderLineStatus.Ordered ? tickets[plan.Item.StationId!.Value] : (Guid?)null;
        await orderAccessor.InsertLineAsync(
            tx,
            tenantId,
            plan.Id,
            storeId,
            orderId,
            visitId,
            lineNo,
            plan.Item.Id,
            plan.Item.Code,
            plan.Item.Name,
            JsonSerializer.Serialize(plan.Tags, JsonDefaults.Options),
            plan.Quantity,
            plan.UnitPrice,
            plan.UnitPrice * plan.Quantity,
            plan.Item.TaxRate,
            plan.Timing,
            plan.Status,
            plan.Item.StationId,
            plan.Item.ServedBy,
            ticketId,
            plan.Status == OrderLineStatus.Held ? null : now,
            plan.Status == OrderLineStatus.Ready ? now : null,
            plan.Status == OrderLineStatus.Served ? now : null,
            cancellationToken);
        for (var i = 0; i < plan.Options.Count; i++)
        {
            var (group, option) = plan.Options[i];
            await orderAccessor.InsertLineOptionAsync(tx, tenantId, plan.Id, i + 1, group.Id, option.Id, option.Name, option.PriceDelta, cancellationToken);
        }
    }

    //--------------------------------------------------------------------------------
    // Release
    //--------------------------------------------------------------------------------

    // 食後の品をお願いする (Held を作る品は Ordered にしてチケットを作る)。Id を送らなければ来店のすべての Held
    public ValueTask<ServiceResult<OrderListResponse>> ReleaseAsync(Guid visitId, OrderReleaseRequest request, CancellationToken cancellationToken)
    {
        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var lineIds = RequestValues.ListOf(request.LineIds);
        return eventService.WriteAsync<ServiceResult<OrderListResponse>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var visit = await visitAccessor.QueryAsync(tx, tenantId, storeId, visitId, cancellationToken);
            if (visit is null)
            {
                return new(ServiceError.NotFound);
            }

            if (!VisitService.InScope(context, visit))
            {
                return new(ServiceError.DeviceScope);
            }

            if (visit.Status is not (VisitStatus.Open or VisitStatus.Paying))
            {
                return new(new ServiceError(ErrorCodes.VisitNotOpen));
            }

            if (await ReleaseHeldAsync(transaction, visit, lineIds, context.Now, cancellationToken) is { } error)
            {
                return new(error);
            }

            var orders = await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, visitId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(new OrderListResponse { Items = orders });
        }, cancellationToken);
    }

    // 止めている食後の品を出す (lineIds が空なら来店のすべて)。会計を始めたときにも、払った品を作るために残りをすべて出す
    // 出す品がなければ何も書かない。来店の状態は呼ぶ側で確かめる
    internal async ValueTask<ServiceError?> ReleaseHeldAsync(StoreTransaction transaction, VisitEntity visit, IReadOnlyList<Guid> lineIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tx = transaction.Tx;
        var tenantId = transaction.TenantId;
        var storeId = transaction.StoreId;
        var held = (await orderAccessor.QueryLineListAsync(tx, tenantId, visit.Id, cancellationToken))
            .Where(x => (x.Status == OrderLineStatus.Held) && ((lineIds.Count == 0) || lineIds.Contains(x.Id)))
            .ToList();
        if (held.Count == 0)
        {
            return null;
        }

        // チケットは注文と持ち場ごと
        var tickets = new Dictionary<(Guid OrderId, Guid StationId), Guid>();
        foreach (var line in held)
        {
            var status = StatusOnRelease(line.ServedBy, line.StationId);
            Guid? ticketId = null;
            if (status == OrderLineStatus.Ordered)
            {
                var key = (line.OrderId, StationId: line.StationId!.Value);
                if (!tickets.TryGetValue(key, out var id))
                {
                    id = Guid.CreateVersion7(now);
                    tickets[key] = id;
                    await kitchenAccessor.InsertTicketAsync(tx, tenantId, id, storeId, key.StationId, key.OrderId, visit.Id, now, cancellationToken);
                }

                ticketId = id;
            }

            var released = await orderAccessor.UpdateLineReleasedAsync(
                tx,
                tenantId,
                line.Id,
                status,
                ticketId,
                status == OrderLineStatus.Ready ? now : null,
                status == OrderLineStatus.Served ? now : null,
                now,
                cancellationToken);
            if (released == 0)
            {
                return new ServiceError(ErrorCodes.LineStatusInvalid);
            }
        }

        var orders = await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, visit.Id, cancellationToken);
        var changedOrders = held.Select(static x => x.OrderId).ToHashSet();
        await transaction.AppendEventAsync(EventTypes.OrderLinesUpdated, LinesUpdated(visit.Id, orders.Where(x => changedOrders.Contains(x.Id))), [visit.TableId], null, now, cancellationToken);
        foreach (var ((_, stationId), ticketId) in tickets)
        {
            var ticket = await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken);
            await transaction.AppendEventAsync(EventTypes.TicketCreated, ticket, null, stationId, now, cancellationToken);
        }

        return null;
    }

    //--------------------------------------------------------------------------------
    // Cancel
    //--------------------------------------------------------------------------------

    // 明細の取消 (ホール)。数量の一部の取消は、取り消す分を別の明細に分けて取り消す
    // 取消で残りの数は戻さない (戻すときは品切れの設定で直す)
    public async ValueTask<ServiceResult<OrderListResponseItem>> CancelLineAsync(Guid orderId, Guid lineId, OrderLineCancelRequest request, CancellationToken cancellationToken)
    {
        if (request.Quantity < 1)
        {
            return new(ServiceError.Validation("quantity", "取り消す数量を 1 以上で送ってください"));
        }

        if (request.Reason?.Length > Length.CancelReason)
        {
            return new(ServiceError.Validation("reason", $"取消の理由を {Length.CancelReason} 文字までで送ってください"));
        }

        if (request.StaffId?.Length > Length.StaffId)
        {
            return new(ServiceError.Validation("staffId", $"スタッフの Id を {Length.StaffId} 文字までで送ってください"));
        }

        var context = contextProvider.Current;
        var tenantId = context.RequireTenantId();
        var storeId = context.RequireStoreId();
        var now = context.Now;
        return await eventService.WriteAsync<ServiceResult<OrderListResponseItem>>(tenantId, storeId, async transaction =>
        {
            var tx = transaction.Tx;
            var line = await orderAccessor.QueryLineAsync(tx, tenantId, storeId, lineId, cancellationToken);
            if ((line is null) || (line.OrderId != orderId))
            {
                return new(ServiceError.NotFound);
            }

            var visit = await visitAccessor.QueryAsync(tx, tenantId, storeId, line.VisitId, cancellationToken);
            if (visit!.Status != VisitStatus.Open)
            {
                return new(new ServiceError(visit.Status == VisitStatus.Paying ? ErrorCodes.CheckoutInProgress : ErrorCodes.VisitNotOpen));
            }

            if (line.Status is OrderLineStatus.Served or OrderLineStatus.Cancelled)
            {
                return new(new ServiceError(ErrorCodes.LineStatusInvalid));
            }

            if (request.Quantity > line.Quantity)
            {
                return new(ServiceError.Validation("quantity", $"取り消す数量を明細の数量 ({line.Quantity}) までで送ってください"));
            }

            int changed;
            if (request.Quantity == line.Quantity)
            {
                changed = await orderAccessor.UpdateLineCancelledAsync(tx, tenantId, lineId, request.Reason, request.StaffId, now, cancellationToken);
            }
            else
            {
                var splitId = Guid.CreateVersion7(now);
                var lineNo = (int)await orderAccessor.QueryNextLineNoAsync(tx, tenantId, orderId, cancellationToken);
                await orderAccessor.InsertLineSplitAsync(tx, tenantId, splitId, lineId, lineNo, request.Quantity, request.Reason, request.StaffId, now, cancellationToken);
                await orderAccessor.InsertLineOptionSplitAsync(tx, tenantId, splitId, lineId, cancellationToken);
                changed = await orderAccessor.AddLineQuantityAsync(tx, tenantId, lineId, -request.Quantity, cancellationToken);
            }

            if (changed == 0)
            {
                return new(new ServiceError(ErrorCodes.LineStatusInvalid));
            }

            var order = (await OrderResponses.LoadAsync(orderAccessor, tx, tenantId, line.VisitId, cancellationToken)).First(x => x.Id == orderId);
            await transaction.AppendEventAsync(EventTypes.OrderLinesUpdated, LinesUpdated(line.VisitId, [order]), [visit.TableId], null, now, cancellationToken);
            if (line.TicketId is { } ticketId)
            {
                var ticket = await OrderResponses.LoadTicketAsync(kitchenAccessor, tx, tenantId, storeId, ticketId, cancellationToken);
                await transaction.AppendEventAsync(EventTypes.TicketUpdated, ticket, null, ticket.StationId, now, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return new(order);
        }, cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Validate
    //--------------------------------------------------------------------------------

    // 受け付ける明細 (メニューで確かめた値と、受けたときの状態)
    private sealed record LinePlan(
        Guid Id,
        MenuResponseItem Item,
        IReadOnlyList<(MenuResponseOptionGroup Group, MenuResponseOption Option)> Options,
        int Quantity,
        decimal UnitPrice,
        OrderTiming Timing,
        OrderLineStatus Status,
        IReadOnlyList<string> Tags);

    // 要求の形 (400)
    private static ServiceError? ValidateRequest(OrderCreateRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Id == Guid.Empty)
        {
            errors["id"] = ["注文の Id を送ってください"];
        }

        if (String.IsNullOrEmpty(request.MenuVersion))
        {
            errors["menuVersion"] = ["表示していたメニューの版を送ってください"];
        }

        // 一覧の要素の null は JSON の読み込みを通るので、ほかの確かめの前に断る
        var lines = RequestValues.ListOf(request.Lines);
        if (lines.Count == 0)
        {
            errors["lines"] = ["明細を 1 つ以上送ってください"];
        }
        else if (RequestValues.HasNull(lines))
        {
            errors["lines"] = ["明細に null を送らないでください"];
        }
        else
        {
            if (lines.Any(static x => x.Id == Guid.Empty) || (lines.Select(static x => x.Id).Distinct().Count() != lines.Count))
            {
                errors["lines"] = ["明細の Id を重ならないように送ってください"];
            }

            foreach (var line in lines.Where(static x => (x.Quantity < 1) || !Enum.IsDefined(x.Timing)))
            {
                errors[line.Id.ToString()] = ["数量 (1 以上) と出す時機を確かめてください"];
            }
        }

        return errors.Count > 0 ? new ServiceError(ErrorCodes.ValidationError, errors) : null;
    }

    // メニュー・オプション・数量を確かめて、受け付ける明細にする
    private static ServiceError? Plan(OrderCreateRequest request, StoreEntity store, MenuCatalog catalog, List<LinePlan> plans)
    {
        if (request.Lines.Count > store.MaxLinesPerOrder)
        {
            return new ServiceError(ErrorCodes.QuantityExceeded, new Dictionary<string, string[]> { ["lines"] = [$"明細は {store.MaxLinesPerOrder} までです"] });
        }

        var changed = new Dictionary<string, string[]>();
        var invalid = new Dictionary<string, string[]>();
        var exceeded = new Dictionary<string, string[]>();
        foreach (var line in request.Lines)
        {
            var key = line.Id.ToString();
            if (!catalog.Items.TryGetValue(line.ItemId, out var item))
            {
                changed[key] = ["メニューにない商品です"];
                continue;
            }

            var optionIds = RequestValues.ListOf(line.OptionIds);
            var options = new List<(MenuResponseOptionGroup Group, MenuResponseOption Option)>();
            foreach (var optionId in optionIds)
            {
                if (catalog.Options.TryGetValue(optionId, out var option))
                {
                    options.Add(option);
                }
            }

            if (options.Count != optionIds.Count)
            {
                changed[key] = ["メニューにないオプションです"];
                continue;
            }

            if (!IsValidSelection(item, optionIds, options, catalog))
            {
                invalid[key] = ["オプションの選び方を確かめてください"];
                continue;
            }

            var unitPrice = Pricing.UnitPrice(item.Price, options.Select(static x => x.Option.PriceDelta));
            if (unitPrice != line.UnitPrice)
            {
                changed[key] = ["価格が変わりました"];
                continue;
            }

            var max = Math.Min(store.MaxQuantityPerLine, item.MaxQuantity ?? Int32.MaxValue);
            if (line.Quantity > max)
            {
                exceeded[key] = [$"数量は {max} までです"];
                continue;
            }

            var timing = item.TimingSelectable ? line.Timing : item.DefaultTiming;
            var tags = item.Tags.Concat(options.SelectMany(static x => x.Option.Tags)).Distinct(StringComparer.Ordinal).ToList();
            plans.Add(new LinePlan(line.Id, item, options, line.Quantity, unitPrice, timing, StatusOnOrder(timing, item.ServedBy, item.StationId), tags));
        }

        if (changed.Count > 0)
        {
            return new ServiceError(ErrorCodes.MenuChanged, changed);
        }

        if (invalid.Count > 0)
        {
            return new ServiceError(ErrorCodes.OptionInvalid, invalid);
        }

        return exceeded.Count > 0 ? new ServiceError(ErrorCodes.QuantityExceeded, exceeded) : null;
    }

    // オプションは商品の組のものを重ねずに選び、組ごとに選ぶ数の範囲に入っていること
    private static bool IsValidSelection(MenuResponseItem item, IReadOnlyList<Guid> optionIds, List<(MenuResponseOptionGroup Group, MenuResponseOption Option)> options, MenuCatalog catalog)
    {
        if ((optionIds.Distinct().Count() != optionIds.Count) || options.Any(x => !item.OptionGroupIds.Contains(x.Group.Id)))
        {
            return false;
        }

        foreach (var groupId in item.OptionGroupIds)
        {
            if (catalog.OptionGroups.TryGetValue(groupId, out var group))
            {
                var count = options.Count(x => x.Group.Id == groupId);
                if ((count < group.MinSelect) || (count > group.MaxSelect))
                {
                    return false;
                }
            }
        }

        return true;
    }

    // 出せる条件 (時間帯、子どもがいる)。時間帯は受けた時刻 (店舗の現地時刻) で確かめ、終わりから grace のうちに届いた注文は受ける (確定を押したあとの通信の遅れ)
    private static ServiceError? CheckAvailability(IEnumerable<LinePlan> plans, MenuCatalog catalog, VisitEntity visit, TimeOnly now, TimeSpan grace)
    {
        if (catalog.AvailabilityRules.Count == 0)
        {
            return null;
        }

        var unavailable = new Dictionary<string, string[]>();
        foreach (var plan in plans)
        {
            var reason = catalog.AvailabilityRules
                .Where(x => plan.Tags.Contains(x.TargetTag, StringComparer.Ordinal))
                .Select(x => TagRules.CheckAvailability(catalog.PeriodsOf(x), x.RequiresChildren == true, now, visit.Children, grace))
                .FirstOrDefault(static x => x != UnavailableReason.None);
            if (reason != UnavailableReason.None)
            {
                unavailable[plan.Id.ToString()] = [reason == UnavailableReason.Children ? "お子様のいる来店だけの商品です" : "出している時間の外の商品です"];
            }
        }

        return unavailable.Count > 0 ? new ServiceError(ErrorCodes.ItemUnavailable, unavailable) : null;
    }

    // 品切れと残りの数 (同じ品の明細は合わせて数える)
    private static ServiceError? CheckStock(IEnumerable<LinePlan> plans, Dictionary<Guid, StockEntity> stocks)
    {
        var soldOut = new Dictionary<string, string[]>();
        var insufficient = new Dictionary<string, string[]>();
        var usage = plans
            .SelectMany(static x => x.Options.Select(static o => o.Option.Id).Prepend(x.Item.Id).Select(id => (TargetId: id, x.Quantity, LineId: x.Id)))
            .GroupBy(static x => x.TargetId);
        foreach (var target in usage)
        {
            if (!stocks.TryGetValue(target.Key, out var stock))
            {
                continue;
            }

            if (stock.Status == StockStatus.SoldOut)
            {
                foreach (var (_, _, lineId) in target)
                {
                    soldOut[lineId.ToString()] = ["売り切れました"];
                }
            }
            else if ((stock.Status == StockStatus.Limited) && (target.Sum(static x => x.Quantity) > (stock.Remaining ?? 0)))
            {
                foreach (var (_, _, lineId) in target)
                {
                    insufficient[lineId.ToString()] = [$"残り {stock.Remaining ?? 0} 点です"];
                }
            }
        }

        if (soldOut.Count > 0)
        {
            return new ServiceError(ErrorCodes.ItemSoldOut, soldOut);
        }

        return insufficient.Count > 0 ? new ServiceError(ErrorCodes.StockInsufficient, insufficient) : null;
    }

    // 確認のルールは来店で答えていること、上限のルールは範囲 (注文、来店、1 人あたり) の数を超えないこと
    private static ServiceError? CheckRules(List<LinePlan> plans, MenuCatalog catalog, VisitEntity visit, List<OrderLineEntity> ordered, List<VisitConfirmationEntity> confirmations)
    {
        var unconfirmed = new Dictionary<string, string[]>();
        var exceeded = new Dictionary<string, string[]>();
        var guests = visit.Adults + visit.Children;
        foreach (var rule in catalog.Menu.Rules)
        {
            var targets = plans.Where(x => x.Tags.Contains(rule.TargetTag, StringComparer.Ordinal)).ToList();
            if (targets.Count == 0)
            {
                continue;
            }

            if ((rule.Kind == MenuRuleKind.Confirmation) && confirmations.All(x => x.RuleId != rule.Id))
            {
                foreach (var target in targets)
                {
                    unconfirmed[target.Id.ToString()] = [rule.Message?.Ja ?? "確認が必要な商品です"];
                }
            }

            if ((rule.Kind == MenuRuleKind.Limit) && (rule.Max is { } max))
            {
                var scope = rule.Scope ?? RuleScope.Order;
                var count = targets.Sum(static x => x.Quantity);
                if (scope != RuleScope.Order)
                {
                    count += ordered
                        .Where(x => (x.Status != OrderLineStatus.Cancelled) && TagsOf(x).Contains(rule.TargetTag, StringComparer.Ordinal))
                        .Sum(static x => x.Quantity);
                }

                if (count > TagRules.Allowance(scope, max, guests))
                {
                    foreach (var target in targets)
                    {
                        exceeded[target.Id.ToString()] = ["数の上限を超えています"];
                    }
                }
            }
        }

        if (unconfirmed.Count > 0)
        {
            return new ServiceError(ErrorCodes.ConfirmationRequired, unconfirmed);
        }

        return exceeded.Count > 0 ? new ServiceError(ErrorCodes.LimitExceeded, exceeded) : null;
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    internal static bool IsAfterLastOrder(StoreEntity store, DateTimeOffset now) =>
        StoreHours.IsAfterLastOrder(now, store.TimeZone, StoreHours.Parse(store.OpenTime), store.LastOrderTime is { } last ? StoreHours.Parse(last) : null);

    // 受けたときの状態。食後の品は止め、お客様がとる品は提供済み、作らない品はできあがりにする
    private static OrderLineStatus StatusOnOrder(OrderTiming timing, ServedBy servedBy, Guid? stationId) =>
        timing == OrderTiming.AfterMeal ? OrderLineStatus.Held : StatusOnRelease(servedBy, stationId);

    private static OrderLineStatus StatusOnRelease(ServedBy servedBy, Guid? stationId)
    {
        if (servedBy == ServedBy.Guest)
        {
            return OrderLineStatus.Served;
        }

        return stationId is null ? OrderLineStatus.Ready : OrderLineStatus.Ordered;
    }

    // 残りの数を減らす品と数 (商品とオプション)
    private static Dictionary<Guid, int> UsageOf(IEnumerable<LinePlan> plans) =>
        plans
            .SelectMany(static x => x.Options.Select(static o => o.Option.Id).Prepend(x.Item.Id).Select(id => (TargetId: id, x.Quantity)))
            .GroupBy(static x => x.TargetId)
            .ToDictionary(static x => x.Key, static x => x.Sum(static y => y.Quantity));

    private static List<string> TagsOf(OrderLineEntity line) =>
        JsonSerializer.Deserialize<List<string>>(line.Tags, JsonDefaults.Options) ?? [];

    private static OrderLinesUpdatedEventData LinesUpdated(Guid visitId, IEnumerable<OrderListResponseItem> orders) =>
        new()
        {
            VisitId = visitId,
            Orders = orders.ToList()
        };
}
