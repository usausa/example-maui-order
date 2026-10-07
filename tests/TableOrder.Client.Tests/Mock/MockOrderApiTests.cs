namespace TableOrder.Client.Mock;

using System.Threading.Channels;

public sealed class MockOrderApiTests
{
    // オプションを選ばずに頼める品 (カルボナーラ、シーザーサラダ)
    private const string CarbonaraCode = "2001";
    private const string CaesarCode = "3001";

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // 注文の一時停止を知らせ、その間の注文を断る。再開すると同じ注文を受け付ける
    [Fact]
    public async Task OrderingPausedRejectsOrders()
    {
        // Arrange
        var api = CreateApi();
        using var events = new EventReader(api);
        var visit = await StartVisitAsync(api, 2, 0);
        var request = await CreateOrderRequestAsync(api, CarbonaraCode);

        // Act / Assert: 一時停止を知らせ、注文を断る
        api.OrderingPaused = true;
        Assert.True((await events.ReadAsync<StoreUpdatedEvent>()).Store.OrderingPaused);
        var paused = await api.CreateOrderAsync(visit.Id, request, Cancel);
        Assert.Equal(ApiStatus.Rejected, paused.Status);
        Assert.Equal("ORDERING_PAUSED", paused.ErrorCode);

        // Act / Assert: 再開を知らせ、同じ注文を受け付ける
        api.OrderingPaused = false;
        Assert.False((await events.ReadAsync<StoreUpdatedEvent>()).Store.OrderingPaused);
        var resumed = await api.CreateOrderAsync(visit.Id, request, Cancel);
        Assert.True(resumed.IsSuccess);
    }

    // ラストオーダーの時刻を知らせ、その前は注文を受け付け、過ぎたら断る
    [Fact]
    public async Task LastOrderPassedRejectsOrders()
    {
        // Arrange
        var api = CreateApi();
        using var events = new EventReader(api);
        var visit = await StartVisitAsync(api, 2, 0);

        // Act / Assert: まもなく (15 分後) は受け付ける
        api.LastOrder = MockLastOrder.Soon;
        Assert.NotNull((await events.ReadAsync<StoreUpdatedEvent>()).Store.LastOrderTime);
        var soon = await api.CreateOrderAsync(visit.Id, await CreateOrderRequestAsync(api, CarbonaraCode), Cancel);
        Assert.True(soon.IsSuccess);

        // Act / Assert: 過ぎたら断る
        api.LastOrder = MockLastOrder.Passed;
        Assert.NotNull((await events.ReadAsync<StoreUpdatedEvent>()).Store.LastOrderTime);
        var passed = await api.CreateOrderAsync(visit.Id, await CreateOrderRequestAsync(api, CaesarCode), Cancel);
        Assert.Equal(ApiStatus.Rejected, passed.Status);
        Assert.Equal("LAST_ORDER_PASSED", passed.ErrorCode);

        // Act / Assert: なしに戻すと受け付ける
        api.LastOrder = MockLastOrder.None;
        Assert.Null((await events.ReadAsync<StoreUpdatedEvent>()).Store.LastOrderTime);
        var none = await api.CreateOrderAsync(visit.Id, await CreateOrderRequestAsync(api, CaesarCode), Cancel);
        Assert.True(none.IsSuccess);
    }

    //--------------------------------------------------------------------------------
    // Payment
    //--------------------------------------------------------------------------------

    // 割り勘: 残りを残りの人数で割った 1 人分ずつ払う。払い終えるまでは残りが減るだけで、払い終えたときに来店が閉じて知らせる
    [Fact]
    public async Task SplitPaymentClosesVisitWhenFullyPaid()
    {
        // Arrange
        var api = CreateApi();
        using var events = new EventReader(api);
        var visit = await StartVisitAsync(api, 2, 1);
        await OrderAsync(api, visit.Id, CarbonaraCode);
        await OrderAsync(api, visit.Id, CaesarCode);
        var balance = (await StartCheckoutAsync(api, visit.Id)).Balance;

        // Act / Assert: 3 人のうち 2 人が払っても、来店は閉じない
        for (var guests = 3; guests > 1; guests--)
        {
            var amount = Pricing.Split(balance, guests)[0];
            await PayAsync(api, visit.Id, amount);
            var bill = await GetBillAsync(api, visit.Id);
            Assert.Equal(balance - amount, bill.Balance);
            Assert.Equal(VisitStatus.Paying, (await GetCurrentVisitAsync(api))?.Status);
            balance = bill.Balance;
        }

        // Act / Assert: 最後の 1 人が残りを払うと来店が閉じ、知らせる (通知は起きた順に届くので、最初に届けば途中で知らせていない)
        await PayAsync(api, visit.Id, balance);
        var closed = await events.ReadAsync<VisitClosedEvent>();
        Assert.Equal(visit.Id, closed.Visit.Id);
        Assert.Equal(VisitStatus.Closed, closed.Visit.Status);
        Assert.Null(await GetCurrentVisitAsync(api));
    }

    // 0 円と、払った分を引いた残りを超える額の支払は断る
    [Fact]
    public async Task CreatePaymentRejectsInvalidAmount()
    {
        // Arrange
        var api = CreateApi();
        var visit = await StartVisitAsync(api, 2, 0);
        await OrderAsync(api, visit.Id, CarbonaraCode);
        var total = (await StartCheckoutAsync(api, visit.Id)).Balance;
        await PayAsync(api, visit.Id, 100m);

        // Act
        var zero = await CreatePaymentAsync(api, visit.Id, 0m);
        var over = await CreatePaymentAsync(api, visit.Id, total - 100m + 1m);

        // Assert
        Assert.Equal(ApiStatus.Rejected, zero.Status);
        Assert.Equal("PAYMENT_AMOUNT_INVALID", zero.ErrorCode);
        Assert.Equal(ApiStatus.Rejected, over.Status);
        Assert.Equal("PAYMENT_AMOUNT_INVALID", over.ErrorCode);
    }

    //--------------------------------------------------------------------------------
    // Event
    //--------------------------------------------------------------------------------

    // ホールで来店を開くと知らせる。来店がある間は開かない
    [Fact]
    public async Task OpenVisitRaisesVisitOpened()
    {
        // Arrange
        var api = CreateApi();
        using var events = new EventReader(api);

        // Act / Assert: 来店を開いて知らせる
        Assert.True(api.OpenVisit(2, 1));
        var opened = await events.ReadAsync<VisitOpenedEvent>();
        Assert.Equal(VisitOpenedBy.Hall, opened.Visit.OpenedBy);
        Assert.Equal(2, opened.Visit.Adults);
        Assert.Equal(1, opened.Visit.Children);
        Assert.Equal(opened.Visit.Id, (await GetCurrentVisitAsync(api))?.Id);

        // Act / Assert: 来店がある間は開かない (知らせていなければ、次に閉じた知らせが届く)
        Assert.False(api.OpenVisit(1, 0));
        Assert.True(api.CloseVisit());
        Assert.Equal(opened.Visit.Id, (await events.ReadAsync<VisitClosedEvent>()).Visit.Id);
    }

    // レジで会計すると来店が閉じて知らせる。来店がなければ閉じない
    [Fact]
    public async Task CloseVisitRaisesVisitClosed()
    {
        // Arrange
        var api = CreateApi();
        using var events = new EventReader(api);
        var visit = await StartVisitAsync(api, 2, 0);

        // Act / Assert: 来店を閉じて知らせる
        Assert.True(api.CloseVisit());
        var closed = await events.ReadAsync<VisitClosedEvent>();
        Assert.Equal(visit.Id, closed.Visit.Id);
        Assert.Equal(VisitStatus.Closed, closed.Visit.Status);
        Assert.Null(await GetCurrentVisitAsync(api));

        // Act / Assert: 来店がなければ閉じない
        Assert.False(api.CloseVisit());
    }

    // 通知は seq の順に届く。遅らせて送る通知 (スタッフメニューの操作) を、後から起きた通知 (払い終えて来店が閉じた) が追い越さない
    [Fact]
    public async Task EventsArriveInSeqOrder()
    {
        // Arrange
        var api = new MockOrderApi
        {
            Latency = TimeSpan.Zero,
            PaymentAfter = TimeSpan.Zero,
            EventDelay = TimeSpan.FromMilliseconds(200)
        };
        using var events = new EventReader(api);
        var visit = await StartVisitAsync(api, 1, 0);
        await OrderAsync(api, visit.Id, CaesarCode);
        var balance = (await StartCheckoutAsync(api, visit.Id)).Balance;

        // Act
        api.OrderingPaused = true;
        await PayAsync(api, visit.Id, balance);
        var paused = await events.ReadAsync<StoreUpdatedEvent>();
        var closed = await events.ReadAsync<VisitClosedEvent>();

        // Assert
        Assert.True(paused.Seq < closed.Seq);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 待ち時間をなくしたモック (支払は次の要求で終わり、通知はすぐ送る)
    private static MockOrderApi CreateApi() =>
        new()
        {
            Latency = TimeSpan.Zero,
            PaymentAfter = TimeSpan.Zero,
            EventDelay = TimeSpan.Zero
        };

    private static async Task<VisitResponse> StartVisitAsync(MockOrderApi api, int adults, int children)
    {
        var result = await api.StartVisitAsync(new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = adults, Children = children }, Cancel);
        Assert.True(result.IsSuccess);
        return result.Content!;
    }

    private static async Task<VisitResponse?> GetCurrentVisitAsync(MockOrderApi api) =>
        (await api.GetCurrentVisitAsync(Cancel)).Content;

    // メニューのコードの品を 1 つ、オプションなしで頼む
    private static async Task<OrderCreateRequest> CreateOrderRequestAsync(MockOrderApi api, string code)
    {
        var menu = (await api.GetMenuAsync(Cancel)).Content!;
        var item = menu.Items.First(x => x.Code == code);
        return new OrderCreateRequest
        {
            Id = Guid.CreateVersion7(),
            MenuVersion = menu.MenuVersion,
            Lines =
            [
                new OrderCreateRequestLine
                {
                    Id = Guid.CreateVersion7(),
                    ItemId = item.Id,
                    OptionIds = [],
                    Quantity = 1,
                    UnitPrice = item.Price,
                    Timing = item.DefaultTiming
                }
            ]
        };
    }

    private static async Task OrderAsync(MockOrderApi api, Guid visitId, string code)
    {
        var result = await api.CreateOrderAsync(visitId, await CreateOrderRequestAsync(api, code), Cancel);
        Assert.True(result.IsSuccess);
    }

    // お会計を始め、始めたときの明細を返す
    private static async Task<BillResponse> StartCheckoutAsync(MockOrderApi api, Guid visitId)
    {
        var bill = await GetBillAsync(api, visitId);
        var result = await api.StartCheckoutAsync(visitId, new CheckoutRequest { BillVersion = bill.BillVersion }, Cancel);
        Assert.True(result.IsSuccess);
        return bill;
    }

    private static async Task<BillResponse> GetBillAsync(MockOrderApi api, Guid visitId)
    {
        var result = await api.GetBillAsync(visitId, Cancel);
        Assert.True(result.IsSuccess);
        return result.Content!;
    }

    private static ValueTask<ApiResult<PaymentResponse>> CreatePaymentAsync(MockOrderApi api, Guid visitId, decimal amount) =>
        api.CreatePaymentAsync(visitId, new PaymentCreateRequest { Id = Guid.CreateVersion7(), Method = PaymentMethod.QrCode, Amount = amount }, Cancel);

    // 支払を作り、終わったことを確かめる (支払の時間は 0 なので、次の要求で終わる)
    private static async Task PayAsync(MockOrderApi api, Guid visitId, decimal amount)
    {
        var created = await CreatePaymentAsync(api, visitId, amount);
        Assert.True(created.IsSuccess);
        var payment = await api.GetPaymentAsync(created.Content!.Id, Cancel);
        Assert.Equal(PaymentStatus.Completed, payment.Content?.Status);
    }

    // 通知を届いた順に読む。通知は別のスレッドで届くので、時間を区切って待つ
    private sealed class EventReader : IDisposable
    {
        private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(5);

        private readonly IOrderEvents events;

        private readonly Channel<OrderEvent> channel = Channel.CreateUnbounded<OrderEvent>();

        public EventReader(IOrderEvents events)
        {
            this.events = events;
            events.Received += HandleReceived;
        }

        public void Dispose()
        {
            events.Received -= HandleReceived;
        }

        // 次に届いた通知を読み、種類を確かめる
        public async Task<T> ReadAsync<T>()
            where T : OrderEvent
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
            timeout.CancelAfter(WaitLimit);
            return Assert.IsType<T>(await channel.Reader.ReadAsync(timeout.Token));
        }

        private void HandleReceived(object? sender, OrderEventArgs args) =>
            channel.Writer.TryWrite(args.Event);
    }
}
