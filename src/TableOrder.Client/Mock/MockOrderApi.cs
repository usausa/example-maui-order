namespace TableOrder.Client.Mock;

// 注文サーバの代わり。来店・注文・呼び出し・支払をメモリに持ち、時間の経過でキッチン・ホール・決済サービスの動きを真似る
// スタッフメニューから障害 (通信できない、支払の失敗、売り切れ) と注文の進み具合、ホール端末やレジの操作 (通知) を起こせる
// 待ち時間 (通信の遅れ、支払、通知) は作るときに替えられる (テストは 0 にして待たずに進める)
public sealed class MockOrderApi : IOrderApi, IOrderEvents, IMockOrderControl
{
    // 注文 (食後の品はお願い) から作り始め・できあがり・提供までの時間
    private static readonly TimeSpan CookingAfter = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ReadyAfter = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ServedAfter = TimeSpan.FromSeconds(25);

    // 呼び出しにスタッフが応えるまでの時間
    private static readonly TimeSpan AcknowledgeAfter = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan QrLifetime = TimeSpan.FromMinutes(5);

    // スタッフメニューから起こすラストオーダーの、今の時刻からのずれ
    private static readonly TimeSpan LastOrderSoon = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LastOrderPassed = TimeSpan.FromMinutes(-1);

    private readonly Lock sync = new();

    private readonly DeviceConfigResponse config = MockData.CreateConfig();

    private readonly MenuResponse menu = MockData.CreateMenu();

    private readonly Dictionary<Guid, MenuResponseItem> items;

    private readonly Dictionary<Guid, MenuResponseOption> options;

    private readonly Dictionary<Guid, Guid> optionGroups;

    private readonly Dictionary<Guid, StockResponseItem> stocks;

    private readonly List<Payment> payments = [];

    private readonly StoreResponse store = MockData.CreateStore(DateTimeOffset.UtcNow);

    // ロックの中で作り、ロックの外で送る通知
    private readonly List<OrderEvent> raising = [];

    // 送っている通知 (次の通知は、これを送り終えてから送る)
    private Task delivering = Task.CompletedTask;

    private Visit? visit;

    private long seq;

    public event EventHandler<OrderEventArgs>? Received;

    public MockOrderApi()
    {
        items = menu.Items.ToDictionary(static x => x.Id);
        options = menu.OptionGroups.SelectMany(static x => x.Options).ToDictionary(static x => x.Id);
        optionGroups = menu.OptionGroups.SelectMany(static g => g.Options.Select(o => (GroupId: g.Id, OptionId: o.Id))).ToDictionary(static x => x.OptionId, static x => x.GroupId);
        stocks = MockData.CreateStock().ToDictionary(static x => x.TargetId);
    }

    //--------------------------------------------------------------------------------
    // Control
    //--------------------------------------------------------------------------------

    // 通信の遅れ
    public TimeSpan Latency { get; init; } = TimeSpan.FromMilliseconds(400);

    // 支払が終わるまでの時間 (QR を読み取る、カードを差し込む)
    public TimeSpan PaymentAfter { get; init; } = TimeSpan.FromSeconds(6);

    public TimeSpan EventDelay { get; init; } = TimeSpan.FromSeconds(3);

    public bool Offline { get; set; }

    public bool FailPayments { get; set; }

    public bool OrderingPaused
    {
        get
        {
            lock (sync)
            {
                return store.OrderingPaused;
            }
        }
        set
        {
            lock (sync)
            {
                store.OrderingPaused = value;
                AddEvent(now => new StoreUpdatedEvent(++seq, now, CopyStore()));
            }

            Raise(EventDelay);
        }
    }

    public MockLastOrder LastOrder
    {
        get;
        set
        {
            lock (sync)
            {
                field = value;
                var now = DateTimeOffset.UtcNow;
                store.LastOrderTime = value switch
                {
                    MockLastOrder.Soon => StoreHours.Format(StoreHours.LocalTime(now + LastOrderSoon, store.TimeZone)),
                    MockLastOrder.Passed => StoreHours.Format(StoreHours.LocalTime(now + LastOrderPassed, store.TimeZone)),
                    _ => null
                };
                AddEvent(at => new StoreUpdatedEvent(++seq, at, CopyStore()));
            }

            Raise(EventDelay);
        }
    }

    public bool OpenVisit(int adults, int children)
    {
        lock (sync)
        {
            if (visit is { Status: VisitStatus.Open or VisitStatus.Paying })
            {
                return false;
            }

            visit = new Visit(Guid.CreateVersion7(), adults, children, VisitOpenedBy.Hall, DateTimeOffset.UtcNow);
            var opened = ToResponse(visit);
            AddEvent(now => new VisitOpenedEvent(++seq, now, opened));
        }

        Raise(EventDelay);
        return true;
    }

    public bool CloseVisit()
    {
        lock (sync)
        {
            if (visit is not { Status: VisitStatus.Open or VisitStatus.Paying } target)
            {
                return false;
            }

            target.Status = VisitStatus.Closed;
            target.Version++;
            var closed = ToResponse(target);
            AddEvent(now => new VisitClosedEvent(++seq, now, closed));
        }

        Raise(EventDelay);
        return true;
    }

    public void SellOut(IEnumerable<Guid> itemIds)
    {
        lock (sync)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var id in itemIds)
            {
                stocks[id] = new StockResponseItem
                {
                    TargetId = id,
                    TargetKind = StockTargetKind.Item,
                    Status = StockStatus.SoldOut,
                    UpdatedAt = now
                };
            }
        }
    }

    public void Restock()
    {
        lock (sync)
        {
            stocks.Clear();
            foreach (var stock in MockData.CreateStock())
            {
                stocks[stock.TargetId] = stock;
            }
        }
    }

    public void AdvanceOrders()
    {
        lock (sync)
        {
            if (visit is null)
            {
                return;
            }

            // 今の段階の次の段階の時間まで、経過した時間を足す
            var now = DateTimeOffset.UtcNow;
            foreach (var line in visit.Orders.SelectMany(static x => x.Lines))
            {
                var next = LineStatus(line, now) switch
                {
                    OrderLineStatus.Ordered => CookingAfter,
                    OrderLineStatus.Cooking => ReadyAfter,
                    OrderLineStatus.Ready => ServedAfter,
                    _ => (TimeSpan?)null
                };
                if (next is { } target)
                {
                    line.Advanced += target - Elapsed(line, now);
                }
            }
        }
    }

    //--------------------------------------------------------------------------------
    // Device / Menu
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<DeviceConfigResponse>> GetConfigAsync(CancellationToken cancel = default) =>
        RespondAsync(() => ApiResult.Success(config), cancel);

    public ValueTask<ApiResult<MenuResponse>> GetMenuAsync(CancellationToken cancel = default) =>
        RespondAsync(() => ApiResult.Success(menu), cancel);

    public ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default) =>
        RespondAsync(() => ApiResult.Success(CopyStore()), cancel);

    public ValueTask<ApiResult<StockResponse>> GetStockAsync(CancellationToken cancel = default) =>
        RespondAsync(() => ApiResult.Success(new StockResponse
        {
            Items = stocks.Values
                .Where(static x => x.Status != StockStatus.Available)
                .Select(static x => new StockResponseItem
                {
                    TargetId = x.TargetId,
                    TargetKind = x.TargetKind,
                    Status = x.Status,
                    Remaining = x.Remaining,
                    UpdatedAt = x.UpdatedAt
                })
                .ToList()
        }), cancel);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<VisitResponse?>> GetCurrentVisitAsync(CancellationToken cancel = default) =>
        RespondAsync(() => ApiResult.Success(visit is { Status: VisitStatus.Open or VisitStatus.Paying } ? ToResponse(visit) : null), cancel);

    public ValueTask<ApiResult<VisitResponse>> StartVisitAsync(VisitCreateRequest request, CancellationToken cancel = default) =>
        RespondAsync(now =>
        {
            if (visit is { Status: VisitStatus.Open or VisitStatus.Paying })
            {
                return visit.Id == request.Id
                    ? ApiResult.Success(ToResponse(visit))
                    : Reject<VisitResponse>("TABLE_OCCUPIED", "このテーブルはご利用中です");
            }

            if ((request.Adults < 0) || (request.Children < 0) || (request.Adults + request.Children < 1))
            {
                return Reject<VisitResponse>("VALIDATION_ERROR", "人数を確かめてください");
            }

            visit = new Visit(request.Id, request.Adults, request.Children, VisitOpenedBy.Table, now);
            return ApiResult.Success(ToResponse(visit));
        }, cancel);

    public ValueTask<ApiResult<VisitResponse>> ConfirmAsync(Guid visitId, VisitConfirmationRequest request, CancellationToken cancel = default) =>
        RespondAsync(_ =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<VisitResponse>("NOT_FOUND", "来店が見つかりません");
            }

            if (target.ConfirmedRuleIds.Add(request.RuleId))
            {
                target.Version++;
            }

            return ApiResult.Success(ToResponse(target));
        }, cancel);

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<OrderListResponseItem>> CreateOrderAsync(Guid visitId, OrderCreateRequest request, CancellationToken cancel = default) =>
        RespondAsync(now =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<OrderListResponseItem>("NOT_FOUND", "来店が見つかりません");
            }

            // 送り直しは受け付けた注文を返す
            if (target.Orders.Find(x => x.Id == request.Id) is { } existing)
            {
                return ApiResult.Success(ToResponse(existing, now));
            }

            if (target.Status != VisitStatus.Open)
            {
                return target.Status == VisitStatus.Paying
                    ? Reject<OrderListResponseItem>("CHECKOUT_IN_PROGRESS", "お会計中は注文できません")
                    : Reject<OrderListResponseItem>("VISIT_NOT_OPEN", "ご来店の受付が終わっています");
            }

            if (store.OrderingPaused)
            {
                return Reject<OrderListResponseItem>("ORDERING_PAUSED", "ただいまご注文を一時停止しています");
            }

            if ((store.LastOrderTime is { } last) &&
                (StoreHours.UntilLastOrder(StoreHours.LocalTime(now, store.TimeZone), StoreHours.Parse(store.OpenTime), StoreHours.Parse(last)) < TimeSpan.Zero))
            {
                return Reject<OrderListResponseItem>("LAST_ORDER_PASSED", "ラストオーダーの時間を過ぎました");
            }

            if (Validate(target, request) is { } error)
            {
                return error;
            }

            var order = new Order(request.Id, target.Orders.Count + 1, now);
            foreach (var line in request.Lines)
            {
                var item = items[line.ItemId];
                var selected = line.OptionIds.Select(x => options[x]).ToList();
                order.Lines.Add(new OrderLine(line.Id, item, selected, line.Quantity, Pricing.UnitPrice(item.Price, selected.Select(static x => x.PriceDelta)), line.Timing, now));

                // 残りの数を減らし、なくなったら売り切れにする
                if (stocks.TryGetValue(line.ItemId, out var stock) && (stock.Status == StockStatus.Limited))
                {
                    stock.Remaining = Math.Max(0, (stock.Remaining ?? 0) - line.Quantity);
                    stock.Status = stock.Remaining > 0 ? StockStatus.Limited : StockStatus.SoldOut;
                    stock.UpdatedAt = now;
                }
            }

            target.Orders.Add(order);
            return ApiResult.Success(ToResponse(order, now));
        }, cancel);

    public ValueTask<ApiResult<OrderListResponse>> GetOrdersAsync(Guid visitId, CancellationToken cancel = default) =>
        RespondAsync(now => FindVisit(visitId) is { } target
            ? ApiResult.Success(ToResponse(target.Orders, now))
            : Reject<OrderListResponse>("NOT_FOUND", "来店が見つかりません"), cancel);

    public ValueTask<ApiResult<OrderListResponse>> ReleaseAsync(Guid visitId, OrderReleaseRequest request, CancellationToken cancel = default) =>
        RespondAsync(now =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<OrderListResponse>("NOT_FOUND", "来店が見つかりません");
            }

            foreach (var line in target.Orders.SelectMany(static x => x.Lines))
            {
                if ((LineStatus(line, now) == OrderLineStatus.Held) && ((request.LineIds.Count == 0) || request.LineIds.Contains(line.Id)))
                {
                    line.ReleasedAt = now;
                }
            }

            return ApiResult.Success(ToResponse(target.Orders, now));
        }, cancel);

    //--------------------------------------------------------------------------------
    // Call
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<CallListResponseItem>> CreateCallAsync(Guid visitId, CallCreateRequest request, CancellationToken cancel = default) =>
        RespondAsync(now =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<CallListResponseItem>("NOT_FOUND", "来店が見つかりません");
            }

            // 同じ用件で開いている呼び出しがあれば、増やさずにそれを返す
            var call = target.Calls.Find(x => (x.Id == request.Id) || ((x.ReasonCode == request.ReasonCode) && (CallStatusOf(x, now) != CallStatus.Done)));
            if (call is null)
            {
                call = new Call(request.Id, request.ReasonCode, now);
                target.Calls.Add(call);
            }

            return ApiResult.Success(ToResponse(call, now));
        }, cancel);

    public ValueTask<ApiResult<CallListResponse>> GetCallsAsync(Guid visitId, CancellationToken cancel = default) =>
        RespondAsync(now => FindVisit(visitId) is { } target
            ? ApiResult.Success(new CallListResponse { Items = target.Calls.Select(x => ToResponse(x, now)).ToList() })
            : Reject<CallListResponse>("NOT_FOUND", "来店が見つかりません"), cancel);

    //--------------------------------------------------------------------------------
    // Bill / Payment
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<BillResponse>> GetBillAsync(Guid visitId, CancellationToken cancel = default) =>
        RespondAsync(now => FindVisit(visitId) is { } target
            ? ApiResult.Success(CreateBill(target, now))
            : Reject<BillResponse>("NOT_FOUND", "来店が見つかりません"), cancel);

    public ValueTask<ApiResult<VisitResponse>> StartCheckoutAsync(Guid visitId, CheckoutRequest request, CancellationToken cancel = default) =>
        RespondAsync(now =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<VisitResponse>("NOT_FOUND", "来店が見つかりません");
            }

            if (target.Status == VisitStatus.Open)
            {
                if (CreateBill(target, now).BillVersion != request.BillVersion)
                {
                    return Reject<VisitResponse>("BILL_CHANGED", "お会計の明細が変わりました");
                }

                target.Status = VisitStatus.Paying;
                target.Version++;
            }

            return target.Status == VisitStatus.Paying
                ? ApiResult.Success(ToResponse(target))
                : Reject<VisitResponse>("VISIT_NOT_OPEN", "ご来店の受付が終わっています");
        }, cancel);

    public ValueTask<ApiResult<VisitResponse>> CancelCheckoutAsync(Guid visitId, CancellationToken cancel = default) =>
        RespondAsync(_ =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<VisitResponse>("NOT_FOUND", "来店が見つかりません");
            }

            // 支払が済んでいなければ注文できる状態に戻す
            if ((target.Status == VisitStatus.Paying) && !payments.Any(x => (x.VisitId == visitId) && (x.Status == PaymentStatus.Completed)))
            {
                foreach (var payment in payments.Where(x => (x.VisitId == visitId) && (x.Status == PaymentStatus.Pending)))
                {
                    payment.Status = PaymentStatus.Cancelled;
                }

                target.Status = VisitStatus.Open;
                target.Version++;
            }

            return ApiResult.Success(ToResponse(target));
        }, cancel);

    public ValueTask<ApiResult<PaymentResponse>> CreatePaymentAsync(Guid visitId, PaymentCreateRequest request, CancellationToken cancel = default) =>
        RespondAsync(now =>
        {
            if (FindVisit(visitId) is not { } target)
            {
                return Reject<PaymentResponse>("NOT_FOUND", "来店が見つかりません");
            }

            if (payments.Find(x => x.Id == request.Id) is { } existing)
            {
                return ApiResult.Success(ToResponse(existing));
            }

            if (target.Status != VisitStatus.Paying)
            {
                return Reject<PaymentResponse>("VISIT_NOT_OPEN", "お会計を始めてください");
            }

            if (!config.PaymentMethods.Contains(request.Method))
            {
                return Reject<PaymentResponse>("PAYMENT_METHOD_UNAVAILABLE", "この支払方法は使えません");
            }

            var bill = CreateBill(target, now);
            if ((request.Amount <= 0) || (request.Amount > bill.Balance))
            {
                return Reject<PaymentResponse>("PAYMENT_AMOUNT_INVALID", "お支払いの額を確かめてください");
            }

            var payment = new Payment(request.Id, visitId, request.Method, request.Amount, now)
            {
                QrCode = request.Method == PaymentMethod.QrCode ? $"https://pay.example.jp/mock/{request.Id:N}" : null,
                ExpiresAt = request.Method == PaymentMethod.QrCode ? now + QrLifetime : null
            };
            payments.Add(payment);
            return ApiResult.Success(ToResponse(payment));
        }, cancel);

    public ValueTask<ApiResult<PaymentResponse>> GetPaymentAsync(Guid paymentId, CancellationToken cancel = default) =>
        RespondAsync(_ => payments.Find(x => x.Id == paymentId) is { } payment
            ? ApiResult.Success(ToResponse(payment))
            : Reject<PaymentResponse>("NOT_FOUND", "支払が見つかりません"), cancel);

    public ValueTask<ApiResult<PaymentResponse>> CancelPaymentAsync(Guid paymentId, CancellationToken cancel = default) =>
        RespondAsync(_ =>
        {
            if (payments.Find(x => x.Id == paymentId) is not { } payment)
            {
                return Reject<PaymentResponse>("NOT_FOUND", "支払が見つかりません");
            }

            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Cancelled;
            }

            return ApiResult.Success(ToResponse(payment));
        }, cancel);

    public ValueTask<ApiResult<ReceiptResponse>> GetReceiptAsync(Guid visitId, CancellationToken cancel = default) =>
        RespondAsync(now => FindVisit(visitId) is { } target
            ? ApiResult.Success(new ReceiptResponse
            {
                Url = new Uri($"https://receipt.example.jp/mock/{visitId:N}"),
                Total = CreateBill(target, now).Total,
                IssuedAt = now
            })
            : Reject<ReceiptResponse>("NOT_FOUND", "来店が見つかりません"), cancel);

    //--------------------------------------------------------------------------------
    // Respond
    //--------------------------------------------------------------------------------

    private ValueTask<ApiResult<T>> RespondAsync<T>(Func<ApiResult<T>> func, CancellationToken cancel) =>
        RespondAsync(_ => func(), cancel);

    // 通信の遅れのあとに、時間の経過 (支払の完了) を反映してから応答を作る
    private async ValueTask<ApiResult<T>> RespondAsync<T>(Func<DateTimeOffset, ApiResult<T>> func, CancellationToken cancel)
    {
        try
        {
            await Task.Delay(Latency, cancel).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return ApiResult.Failure<T>(ApiStatus.Canceled);
        }

        ApiResult<T> result;
        lock (sync)
        {
            if (Offline)
            {
                return ApiResult.Failure<T>(ApiStatus.Unavailable);
            }

            var now = DateTimeOffset.UtcNow;
            Advance(now);
            result = func(now);
        }

        // 時間の経過で起きたこと (テーブルで払い終えて来店が閉じた) は、待たずに知らせる
        Raise(TimeSpan.Zero);
        return result;
    }

    private static ApiResult<T> Reject<T>(string errorCode, string detail) =>
        ApiResult.Failure<T>(ApiStatus.Rejected, errorCode, detail);

    private Visit? FindVisit(Guid visitId) =>
        visit?.Id == visitId ? visit : null;

    // 支払を完了 (支払を失敗させている間は失敗) にし、残りがなくなった来店を終える
    private void Advance(DateTimeOffset now)
    {
        foreach (var payment in payments.Where(x => (x.Status == PaymentStatus.Pending) && (now - x.CreatedAt >= PaymentAfter)))
        {
            if (FailPayments)
            {
                payment.Status = PaymentStatus.Failed;
                continue;
            }

            payment.Status = PaymentStatus.Completed;
            payment.CompletedAt = payment.CreatedAt + PaymentAfter;
        }

        if (visit is { Status: VisitStatus.Paying } paying && (CreateBill(paying, now).Balance <= 0))
        {
            paying.Status = VisitStatus.Closed;
            paying.Version++;
            var closed = ToResponse(paying);
            AddEvent(at => new VisitClosedEvent(++seq, at, closed));
        }
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // ロックの中で通知を作る (seq の順を起きた順にする)
    private void AddEvent(Func<DateTimeOffset, OrderEvent> create) =>
        raising.Add(create(DateTimeOffset.UtcNow));

    // 作った通知を delay の後に送る (スタッフメニューの操作の通知は、お客様の画面に戻る間をとる)
    // 送るのは前の通知を送り終えてから。受け手は seq の古い通知を捨てるので、遅らせた通知を後から起きた通知に追い越させない
    private void Raise(TimeSpan delay)
    {
        lock (sync)
        {
            if (raising.Count == 0)
            {
                return;
            }

            var previous = delivering;
            var events = raising.ToList();
            var due = DateTimeOffset.UtcNow + delay;
            raising.Clear();

            // 受ける側がモックを呼び返してもよいように、ロックの外 (別のスレッド) で送る
            delivering = Task.Run(() => DeliverAsync(previous, events, due));
        }
    }

    private async Task DeliverAsync(Task previous, List<OrderEvent> events, DateTimeOffset due)
    {
        await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        var wait = due - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero)
        {
            await Task.Delay(wait).ConfigureAwait(false);
        }

        foreach (var e in events)
        {
            Received?.Invoke(this, new OrderEventArgs(e));
        }
    }

    private StoreResponse CopyStore() =>
        new()
        {
            Id = store.Id,
            Code = store.Code,
            Name = store.Name,
            TimeZone = store.TimeZone,
            BusinessDate = store.BusinessDate,
            OpenTime = store.OpenTime,
            CloseTime = store.CloseTime,
            LastOrderTime = store.LastOrderTime,
            OrderingPaused = store.OrderingPaused,
            PausedMessage = store.PausedMessage,
            TaxRounding = store.TaxRounding
        };

    //--------------------------------------------------------------------------------
    // Validate
    //--------------------------------------------------------------------------------

    private ApiResult<OrderListResponseItem>? Validate(Visit target, OrderCreateRequest request)
    {
        if (request.Lines.Count == 0)
        {
            return Reject<OrderListResponseItem>("VALIDATION_ERROR", "商品を選んでください");
        }

        foreach (var line in request.Lines)
        {
            if (!items.TryGetValue(line.ItemId, out var item) || !line.OptionIds.All(options.ContainsKey) || (line.Quantity <= 0))
            {
                return Reject<OrderListResponseItem>("VALIDATION_ERROR", "注文の内容を確かめてください");
            }

            if (stocks.TryGetValue(line.ItemId, out var stock))
            {
                if (stock.Status == StockStatus.SoldOut)
                {
                    return Reject<OrderListResponseItem>("ITEM_SOLD_OUT", $"{item.Name.Ja}は売り切れました");
                }

                if ((stock.Status == StockStatus.Limited) && (line.Quantity > stock.Remaining))
                {
                    return Reject<OrderListResponseItem>("STOCK_INSUFFICIENT", $"{item.Name.Ja}は残り {stock.Remaining} 点です");
                }
            }
        }

        // 確認のルールは来店で答えていること、上限のルールは来店のこれまでの注文と合わせて数える
        var ordered = target.Orders.SelectMany(static x => x.Lines).Select(x => (Tags: Tags(x.Item, x.Options), x.Quantity)).ToList();
        var requested = request.Lines.Select(x => (Tags: Tags(items[x.ItemId], x.OptionIds.Select(id => options[id])), x.Quantity)).ToList();
        foreach (var rule in menu.Rules)
        {
            var count = requested.Where(x => x.Tags.Contains(rule.TargetTag)).Sum(static x => x.Quantity);
            if (count == 0)
            {
                continue;
            }

            if ((rule.Kind == MenuRuleKind.Confirmation) && !target.ConfirmedRuleIds.Contains(rule.Id))
            {
                return Reject<OrderListResponseItem>("CONFIRMATION_REQUIRED", rule.Message?.Ja ?? "確認が必要な商品があります");
            }

            if ((rule.Kind == MenuRuleKind.Limit) && (rule.Max is { } max))
            {
                var total = count + ordered.Where(x => x.Tags.Contains(rule.TargetTag)).Sum(static x => x.Quantity);
                if (total > TagRules.Allowance(rule.Scope ?? RuleScope.Order, max, target.Adults + target.Children))
                {
                    return Reject<OrderListResponseItem>("LIMIT_EXCEEDED", "数の上限を超えています");
                }
            }
        }

        return null;
    }

    private static HashSet<string> Tags(MenuResponseItem item, IEnumerable<MenuResponseOption> selected)
    {
        var tags = new HashSet<string>(item.Tags, StringComparer.Ordinal);
        foreach (var option in selected)
        {
            tags.UnionWith(option.Tags);
        }

        return tags;
    }

    //--------------------------------------------------------------------------------
    // Status
    //--------------------------------------------------------------------------------

    // 時間の経過で調理と提供を進める (お客様がとる品は受けたときに提供済み)
    private static OrderLineStatus LineStatus(OrderLine line, DateTimeOffset now)
    {
        if ((line.Timing == OrderTiming.AfterMeal) && (line.ReleasedAt is null))
        {
            return OrderLineStatus.Held;
        }

        if (line.Item.ServedBy == ServedBy.Guest)
        {
            return OrderLineStatus.Served;
        }

        var elapsed = Elapsed(line, now);
        if (elapsed >= ServedAfter)
        {
            return OrderLineStatus.Served;
        }

        if (elapsed >= ReadyAfter)
        {
            return OrderLineStatus.Ready;
        }

        return elapsed >= CookingAfter ? OrderLineStatus.Cooking : OrderLineStatus.Ordered;
    }

    // 注文 (食後の品はお願い) からの時間。スタッフメニューで進めた分を足す
    private static TimeSpan Elapsed(OrderLine line, DateTimeOffset now) =>
        now - (line.ReleasedAt ?? line.OrderedAt) + line.Advanced;

    private static CallStatus CallStatusOf(Call call, DateTimeOffset now) =>
        now - call.CreatedAt >= AcknowledgeAfter ? CallStatus.Acknowledged : CallStatus.Open;

    //--------------------------------------------------------------------------------
    // Bill
    //--------------------------------------------------------------------------------

    private BillResponse CreateBill(Visit target, DateTimeOffset now)
    {
        var lines = target.Orders
            .SelectMany(static x => x.Lines)
            .GroupBy(static x => (x.Item.Id, Options: String.Join(',', x.Options.Select(static o => o.Id)), x.UnitPrice))
            .Select(static g =>
            {
                var first = g.First();
                var quantity = g.Sum(static x => x.Quantity);
                return new BillResponseLine
                {
                    Name = first.Item.Name,
                    Options = first.Options.Select(static x => x.Name).ToList(),
                    Quantity = quantity,
                    UnitPrice = first.UnitPrice,
                    Amount = first.UnitPrice * quantity,
                    TaxRate = first.Item.TaxRate
                };
            })
            .ToList();
        var taxes = lines
            .GroupBy(static x => x.TaxRate)
            .Select(g =>
            {
                var taxable = g.Sum(static x => x.Amount);
                return new BillResponseTax
                {
                    Rate = g.Key,
                    TaxableAmount = taxable,
                    TaxAmount = Pricing.IncludedTax(taxable, g.Key, config.TaxRounding)
                };
            })
            .ToList();
        var total = lines.Sum(static x => x.Amount);
        var paid = payments.Where(x => (x.VisitId == target.Id) && (x.Status == PaymentStatus.Completed)).Sum(static x => x.Amount);
        var guests = target.Adults + target.Children;

        return new BillResponse
        {
            VisitId = target.Id,
            BillVersion = $"{target.Orders.Sum(static x => x.Lines.Count)}-{total}",
            Lines = lines,
            Taxes = taxes,
            Total = total,
            PaidAmount = paid,
            Balance = total - paid,
            Guests = guests,
            SplitAmounts = Pricing.Split(total, guests),
            HasUnservedLines = target.Orders.SelectMany(static x => x.Lines).Any(x => LineStatus(x, now) is not (OrderLineStatus.Served or OrderLineStatus.Cancelled))
        };
    }

    //--------------------------------------------------------------------------------
    // Response
    //--------------------------------------------------------------------------------

    private static VisitResponse ToResponse(Visit source) =>
        new()
        {
            Id = source.Id,
            TableName = string.Empty,
            Adults = source.Adults,
            Children = source.Children,
            Status = source.Status,
            OpenedBy = source.OpenedBy,
            OpenedAt = source.OpenedAt,
            ConfirmedRuleIds = source.ConfirmedRuleIds.ToList(),
            OrderTotal = source.Orders.SelectMany(static x => x.Lines).Sum(static x => x.UnitPrice * x.Quantity),
            Version = source.Version
        };

    private OrderListResponse ToResponse(IEnumerable<Order> orders, DateTimeOffset now) =>
        new()
        {
            Items = orders.Select(x => ToResponse(x, now)).ToList()
        };

    private OrderListResponseItem ToResponse(Order source, DateTimeOffset now) =>
        new()
        {
            Id = source.Id,
            OrderNo = source.OrderNo,
            Source = OrderSource.Table,
            OrderedAt = source.OrderedAt,
            Amount = source.Lines.Sum(static x => x.UnitPrice * x.Quantity),
            Lines = source.Lines.Select(x =>
            {
                var status = LineStatus(x, now);
                return new OrderListResponseLine
                {
                    Id = x.Id,
                    ItemId = x.Item.Id,
                    Name = x.Item.Name,
                    Options = x.Options.Select(o => new OrderListResponseOption
                    {
                        OptionGroupId = optionGroups[o.Id],
                        OptionId = o.Id,
                        Name = o.Name,
                        PriceDelta = o.PriceDelta
                    }).ToList(),
                    Quantity = x.Quantity,
                    UnitPrice = x.UnitPrice,
                    Amount = x.UnitPrice * x.Quantity,
                    TaxRate = x.Item.TaxRate,
                    Timing = x.Timing,
                    Status = status,
                    ServedAt = status == OrderLineStatus.Served ? now : null
                };
            }).ToList()
        };

    private static CallListResponseItem ToResponse(Call source, DateTimeOffset now)
    {
        var status = CallStatusOf(source, now);
        return new CallListResponseItem
        {
            Id = source.Id,
            ReasonCode = source.ReasonCode,
            Status = status,
            CreatedAt = source.CreatedAt,
            AcknowledgedAt = status == CallStatus.Acknowledged ? source.CreatedAt + AcknowledgeAfter : null
        };
    }

    private static PaymentResponse ToResponse(Payment source) =>
        new()
        {
            Id = source.Id,
            Method = source.Method,
            Amount = source.Amount,
            Status = source.Status,
            QrCode = source.QrCode,
            ExpiresAt = source.ExpiresAt,
            CompletedAt = source.CompletedAt
        };

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    private sealed class Visit
    {
        public Guid Id { get; }

        public int Adults { get; }

        public int Children { get; }

        public VisitOpenedBy OpenedBy { get; }

        public DateTimeOffset OpenedAt { get; }

        public VisitStatus Status { get; set; } = VisitStatus.Open;

        public int Version { get; set; } = 1;

        public HashSet<Guid> ConfirmedRuleIds { get; } = [];

        public List<Order> Orders { get; } = [];

        public List<Call> Calls { get; } = [];

        public Visit(Guid id, int adults, int children, VisitOpenedBy openedBy, DateTimeOffset openedAt)
        {
            Id = id;
            Adults = adults;
            Children = children;
            OpenedBy = openedBy;
            OpenedAt = openedAt;
        }
    }

    private sealed class Order
    {
        public Guid Id { get; }

        public int OrderNo { get; }

        public DateTimeOffset OrderedAt { get; }

        public List<OrderLine> Lines { get; } = [];

        public Order(Guid id, int orderNo, DateTimeOffset orderedAt)
        {
            Id = id;
            OrderNo = orderNo;
            OrderedAt = orderedAt;
        }
    }

    private sealed class OrderLine
    {
        public Guid Id { get; }

        public MenuResponseItem Item { get; }

        public IReadOnlyList<MenuResponseOption> Options { get; }

        public int Quantity { get; }

        public decimal UnitPrice { get; }

        public OrderTiming Timing { get; }

        public DateTimeOffset OrderedAt { get; }

        // 食後の品をお願いした時刻
        public DateTimeOffset? ReleasedAt { get; set; }

        // スタッフメニューで進めた時間
        public TimeSpan Advanced { get; set; }

        public OrderLine(Guid id, MenuResponseItem item, IReadOnlyList<MenuResponseOption> options, int quantity, decimal unitPrice, OrderTiming timing, DateTimeOffset orderedAt)
        {
            Id = id;
            Item = item;
            Options = options;
            Quantity = quantity;
            UnitPrice = unitPrice;
            Timing = timing;
            OrderedAt = orderedAt;
        }
    }

    private sealed class Call
    {
        public Guid Id { get; }

        public string ReasonCode { get; }

        public DateTimeOffset CreatedAt { get; }

        public Call(Guid id, string reasonCode, DateTimeOffset createdAt)
        {
            Id = id;
            ReasonCode = reasonCode;
            CreatedAt = createdAt;
        }
    }

    private sealed class Payment
    {
        public Guid Id { get; }

        public Guid VisitId { get; }

        public PaymentMethod Method { get; }

        public decimal Amount { get; }

        public DateTimeOffset CreatedAt { get; }

        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        public string? QrCode { get; init; }

        public DateTimeOffset? ExpiresAt { get; init; }

        public DateTimeOffset? CompletedAt { get; set; }

        public Payment(Guid id, Guid visitId, PaymentMethod method, decimal amount, DateTimeOffset createdAt)
        {
            Id = id;
            VisitId = visitId;
            Method = method;
            Amount = amount;
            CreatedAt = createdAt;
        }
    }
}
